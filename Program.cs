using Scriban;
using SixLabors.ImageSharp;
using System.IO.Compression;
using System.Reflection;

if (args.Length == 0)
{
    Console.WriteLine("Usage:");
    Console.WriteLine("    >Img2Epub TargetDirName");
    return 1;
}

foreach (var srcDir in args)
{
    var dstFile = srcDir + ".epub";
    var title = Path.GetFileName(srcDir);

    var images = Enumerable.Concat(
        Directory.EnumerateFiles(srcDir, "*.jpg", SearchOption.TopDirectoryOnly),
        Directory.EnumerateFiles(srcDir, "*.jpeg", SearchOption.TopDirectoryOnly)
    ).OrderBy(x => x).ToArray();

    var pages = MakePages(title, images);

    using var archive = ZipFile.Open(dstFile, ZipArchiveMode.Create);

    AddString(archive, @"META-INF/container.xml", ReadResource("container.xml"));
    AddString(archive, @"content.opf", MakeContentOpf(title, images));
    AddString(archive, @"mimetype", ReadResource("mimetype"));
    AddString(archive, @"page_styles.css", ReadResource("page_styles.css"));
    AddString(archive, @"stylesheet.css", ReadResource("stylesheet.css"));
    AddString(archive, @"titlepage.xhtml", MakeTitlepageXhtml(images[0]));

    foreach (var page in pages)
        AddString(archive, "text/" + page.Path, page.Text);

    foreach (var image in images)
        AddBinaryFile(archive, "images/" + Path.GetFileName(image), image);
}

return 0;

///////////////////////////////////////////////////////////////////////////////////////
static void AddString(ZipArchive archive, string dstPath, string srcData)
{
    var entity = archive.CreateEntry(dstPath);

    using var sw = new StreamWriter(entity.Open());

    sw.WriteLine(srcData);
}

static void AddBinaryFile(ZipArchive archive, string dstPath, string srcDataFilepath)
{
    archive.CreateEntryFromFile(srcDataFilepath, dstPath);
}

static Page[] MakePages(string title, string[] imageFilepaths)
{
    var template = ReadTemplate("page.html");

    return imageFilepaths.Select(x =>
    {
        var size = ReadImageSize(x);
        var path = "images/" + Path.GetFileName(x);
        var text = template.Render(new { size.Width, size.Height, Path = path, Title = title });

        return new Page(Path.GetFileNameWithoutExtension(x) + ".html", text);
    }).ToArray();
}

static string MakeContentOpf(string title, string[] imageFilepaths)
{
    var template = ReadTemplate("content.opf");

    var index = 0;

    var jpegs = imageFilepaths.Select(x => new { Path = Path.GetFileName(x), Id = index++ }).ToArray();
    var htmls = imageFilepaths.Select(x => new { Path = Path.GetFileNameWithoutExtension(x) + ".html", Id = index++ }).ToArray();

    return template.Render(new { Title = title, Jpegs = jpegs, Htmls = htmls });
}

static string MakeTitlepageXhtml(string imageFilepath)
{
    var template = ReadTemplate("titlepage.xhtml");
    var size = ReadImageSize(imageFilepath);

    var path = "images/" + Path.GetFileName(imageFilepath);

    return template.Render(new { size.Width, size.Height, Path = path });
}

static Template ReadTemplate(string name)
{
    return Template.Parse(ReadResource(name));
}

static string ReadResource(string name)
{
    using var stream = Local.ExecutingAssembly.GetManifestResourceStream("Img2Epub.Resources." + name);

    if (stream == null)
        throw new Exception();

    using var sr = new StreamReader(stream);

    return sr.ReadToEnd();
}

static Size ReadImageSize(string imageFilepath)
{
    using var image = Image.Load(imageFilepath);

    return new Size(image.Width, image.Height);
}

record Size(int Width, int Height);
record Page(string Path, string Text);

class Local
{
    public static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();
}
