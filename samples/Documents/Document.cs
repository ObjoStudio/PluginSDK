using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Objo.Runtime.Abstractions;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;

namespace Acme.Documents;

/// <summary>An editable DOCX document. Child wrappers retain this owner and fail after disposal.</summary>
[ObjoExport]
public sealed class Document : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly WordprocessingDocument _package;
    private readonly Dictionary<OpenXmlElement, object> _wrappers = new(ReferenceEqualityComparer.Instance);
    private bool _disposed;

    private Document(MemoryStream stream, WordprocessingDocument package)
    {
        _stream = stream;
        _package = package;
    }

    /// <summary>Creates an empty editable document in memory.</summary>
    public static Document Create()
    {
        var stream = new MemoryStream();
        WordprocessingDocument? package = null;
        try
        {
            package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true);
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body());
            return new Document(stream, package);
        }
        catch
        {
            package?.Dispose();
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Opens owned DOCX bytes for editing; malformed content fails with the provider's error.</summary>
    public static Document OpenBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var stream = new MemoryStream();
        stream.Write(bytes);
        stream.Position = 0;
        WordprocessingDocument? package = null;
        try
        {
            package = WordprocessingDocument.Open(stream, true);
            if (package.MainDocumentPart?.Document?.Body is null)
                throw new InvalidDataException("The DOCX has no main document body.");
            return new Document(stream, package);
        }
        catch
        {
            package?.Dispose();
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Opens a DOCX file without retaining a file lock.</summary>
    public static Document OpenFile(string path) => OpenBytes(File.ReadAllBytes(path));

    /// <summary>Returns independent DOCX bytes while keeping this document editable.</summary>
    public byte[] SaveBytes()
    {
        RequireLive();
        _package.Save();
        using var output = new MemoryStream();
        using (var clone = (WordprocessingDocument)_package.Clone(output, true))
            clone.Save();
        return output.ToArray();
    }

    /// <summary>Atomically replaces the destination DOCX after a complete write.</summary>
    public void SaveFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var target = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(target)!, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, SaveBytes());
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    /// <summary>Appends a body paragraph containing one text run.</summary>
    public Paragraph AddParagraph(string text)
    {
        var element = new W.Paragraph(new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        Body.InsertBefore(element, Body.GetFirstChild<W.SectionProperties>());
        return WrapParagraph(element);
    }

    /// <summary>Appends a body table.</summary>
    public Table AddTable()
    {
        var element = new W.Table(new W.TableProperties(), new W.TableGrid(new W.GridColumn()));
        Body.InsertBefore(element, Body.GetFirstChild<W.SectionProperties>());
        return WrapTable(element);
    }

    /// <summary>Returns a snapshot of direct body paragraphs in document order.</summary>
    public Paragraph[] Paragraphs() => Body.Elements<W.Paragraph>().Select(WrapParagraph).ToArray();

    /// <summary>Returns a snapshot of direct body tables in document order.</summary>
    public Table[] Tables() => Body.Elements<W.Table>().Select(WrapTable).ToArray();

    /// <summary>Returns a snapshot of inline images in the main document.</summary>
    public Image[] Images()
    {
        var main = MainPart;
        return main.Document!.Descendants<W.Drawing>().Select(drawing =>
        {
            var id = drawing.Descendants<A.Blip>().FirstOrDefault()?.Embed?.Value
                ?? throw new InvalidDataException("The inline image has no package relationship.");
            return WrapImage(drawing, (ImagePart)main.GetPartById(id));
        }).ToArray();
    }

    /// <summary>Number of direct body paragraphs.</summary>
    public long ParagraphCount => Paragraphs().LongLength;

    /// <summary>Returns a direct body paragraph by zero-based index.</summary>
    public Paragraph ParagraphAt(long index) => At(Paragraphs(), index);

    /// <summary>Creates or replaces a named paragraph style.</summary>
    public Style DefineParagraphStyle(string id, string name)
    {
        RequireLive();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A style ID and name are required.");
        var main = _package.MainDocumentPart!;
        var part = main.StyleDefinitionsPart ?? main.AddNewPart<StyleDefinitionsPart>();
        part.Styles ??= new W.Styles();
        var previous = part.Styles.Elements<W.Style>().FirstOrDefault(style => style.StyleId?.Value == id);
        previous?.Remove();
        var element = new W.Style(new W.StyleName { Val = name })
        {
            Type = W.StyleValues.Paragraph,
            StyleId = id,
            CustomStyle = true
        };
        part.Styles.Append(element);
        return WrapStyle(element);
    }

    /// <summary>Finds a paragraph style by identifier, or Nothing when absent.</summary>
    public Style? FindParagraphStyle(string id)
    {
        RequireLive();
        var element = MainPart.StyleDefinitionsPart?.Styles?.Elements<W.Style>()
            .FirstOrDefault(style => style.StyleId?.Value == id);
        return element is null ? null : WrapStyle(element);
    }

    /// <summary>Returns the current section header, creating one if absent.</summary>
    public HeaderFooter Header() => GetHeaderFooter(header: true);

    /// <summary>Returns the current section footer, creating one if absent.</summary>
    public HeaderFooter Footer() => GetHeaderFooter(header: false);

    /// <summary>Replaces one named content-control field; returns false when absent.</summary>
    public bool ReplaceField(string name, string value)
    {
        RequireLive();
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        var fields = MainPart.Document!.Descendants<W.SdtRun>()
            .Where(field => field.SdtProperties?.GetFirstChild<W.Tag>()?.Val?.Value == name).ToArray();
        foreach (var field in fields)
        {
            var content = field.SdtContentRun ?? throw new InvalidDataException("Template field has no content.");
            content.RemoveAllChildren();
            content.Append(new W.Run(new W.Text(value) { Space = SpaceProcessingModeValues.Preserve }));
        }
        return fields.Length > 0;
    }

    /// <summary>Appends a named content-control template field to the body.</summary>
    public Paragraph AddField(string name, string initialValue)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A field name is required.", nameof(name));
        var paragraph = new W.Paragraph(new W.SdtRun(
            new W.SdtProperties(new W.Tag { Val = name }),
            new W.SdtContentRun(new W.Run(new W.Text(initialValue) { Space = SpaceProcessingModeValues.Preserve }))));
        Body.InsertBefore(paragraph, Body.GetFirstChild<W.SectionProperties>());
        return WrapParagraph(paragraph);
    }

    /// <summary>Releases the package and invalidates every child wrapper.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _package.Dispose();
        _stream.Dispose();
        _wrappers.Clear();
    }

    internal MainDocumentPart MainPart { get { RequireLive(); return _package.MainDocumentPart!; } }
    internal W.Body Body { get { RequireLive(); return MainPart.Document!.Body!; } }

    internal void RequireLive()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(Document));
    }

    internal void RequireAttached(OpenXmlElement element)
    {
        RequireLive();
        if (!element.Ancestors().Any(ancestor =>
                ancestor is W.Body or W.Header or W.Footer or W.Styles))
            throw new InvalidOperationException("This document node has been removed.");
    }

    internal Paragraph WrapParagraph(W.Paragraph element) => Wrap(element, () => new Paragraph(this, element));
    internal Run WrapRun(W.Run element) => Wrap(element, () => new Run(this, element));
    internal Table WrapTable(W.Table element) => Wrap(element, () => new Table(this, element));
    internal Row WrapRow(W.TableRow element) => Wrap(element, () => new Row(this, element));
    internal Cell WrapCell(W.TableCell element) => Wrap(element, () => new Cell(this, element));
    internal Image WrapImage(W.Drawing drawing, ImagePart part) => Wrap(drawing, () => new Image(this, drawing, part));
    internal Style WrapStyle(W.Style element) => Wrap(element, () => new Style(this, element));
    internal HeaderFooter WrapHeaderFooter(OpenXmlCompositeElement element, bool header)
    {
        RequireLive();
        if (_wrappers.TryGetValue(element, out var existing)) return (HeaderFooter)existing;
        var wrapper = new HeaderFooter(this, element, header);
        _wrappers.Add(element, wrapper);
        return wrapper;
    }

    private T Wrap<T>(OpenXmlElement element, Func<T> create) where T : class
    {
        RequireAttached(element);
        if (_wrappers.TryGetValue(element, out var existing)) return (T)existing;
        var wrapper = create();
        _wrappers.Add(element, wrapper);
        return wrapper;
    }

    private HeaderFooter GetHeaderFooter(bool header)
    {
        var main = MainPart;
        var section = Body.GetFirstChild<W.SectionProperties>();
        if (section is null)
        {
            section = new W.SectionProperties();
            Body.Append(section);
        }
        var reference = header
            ? (W.HeaderFooterReferenceType?)section.GetFirstChild<W.HeaderReference>()
            : section.GetFirstChild<W.FooterReference>();
        if (reference?.Id?.Value is { } id)
        {
            var existing = main.GetPartById(id);
            return header
                ? WrapHeaderFooter(((HeaderPart)existing).Header
                    ?? throw new InvalidDataException("The DOCX header is missing."), true)
                : WrapHeaderFooter(((FooterPart)existing).Footer
                    ?? throw new InvalidDataException("The DOCX footer is missing."), false);
        }
        if (header)
        {
            var part = main.AddNewPart<HeaderPart>();
            part.Header = new W.Header();
            section.Append(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(part) });
            return WrapHeaderFooter(part.Header, true);
        }
        else
        {
            var part = main.AddNewPart<FooterPart>();
            part.Footer = new W.Footer();
            section.Append(new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(part) });
            return WrapHeaderFooter(part.Footer, false);
        }
    }

    private static T At<T>(T[] values, long index) =>
        index < 0 || index >= values.LongLength ? throw new IndexOutOfRangeException() : values[index];
}
