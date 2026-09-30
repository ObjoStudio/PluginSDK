using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using Objo.Runtime.Abstractions;

namespace Acme.Documents;

/// <summary>A document paragraph whose runs and text can be edited.</summary>
[ObjoExport]
public sealed class Paragraph
{
    private readonly Document _owner;
    private readonly W.Paragraph _element;

    internal Paragraph(Document owner, W.Paragraph element) { _owner = owner; _element = element; }
    private W.Paragraph Live { get { _owner.RequireAttached(_element); return _element; } }

    /// <summary>Concatenated text, excluding encoded images.</summary>
    public string Text => string.Concat(Live.Descendants<W.Text>().Select(text => text.Text));

    /// <summary>Paragraph style identifier, or an empty string for the default style.</summary>
    public string StyleId
    {
        get => Live.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
        set
        {
            Live.ParagraphProperties ??= new W.ParagraphProperties();
            Live.ParagraphProperties.ParagraphStyleId = string.IsNullOrEmpty(value) ? null : new W.ParagraphStyleId { Val = value };
        }
    }

    /// <summary>Appends a text run.</summary>
    public Run AddRun(string text)
    {
        var element = new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
        Live.Append(element);
        return _owner.WrapRun(element);
    }

    /// <summary>Snapshot of direct text/image runs in order.</summary>
    public Run[] Runs() => Live.Elements<W.Run>().Select(_owner.WrapRun).ToArray();

    /// <summary>Run count.</summary>
    public long Count => Runs().LongLength;

    /// <summary>Run by zero-based index.</summary>
    public Run Item(long index)
    {
        var runs = Runs();
        return index < 0 || index >= runs.LongLength ? throw new IndexOutOfRangeException() : runs[index];
    }

    /// <summary>Embeds independent PNG bytes as an inline image.</summary>
    public Image AddPng(byte[] encoded) => AddImage(encoded, png: true);

    /// <summary>Embeds independent JPEG bytes as an inline image.</summary>
    public Image AddJpeg(byte[] encoded) => AddImage(encoded, png: false);

    private Image AddImage(byte[] encoded, bool png)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        if (png ? encoded.Length < 8 || !encoded.AsSpan(0, 8).SequenceEqual(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) :
            encoded.Length < 4 || encoded[0] != 0xff || encoded[1] != 0xd8 ||
            encoded[^2] != 0xff || encoded[^1] != 0xd9)
            throw new InvalidDataException(png ? "The image is not an encoded PNG." :
                "The image is not an encoded JPEG.");
        var main = _owner.MainPart;
        var part = main.AddImagePart(png ? ImagePartType.Png : ImagePartType.Jpeg);
        try
        {
            using (var source = new MemoryStream(encoded, writable: false)) part.FeedData(source);
            var id = main.GetIdOfPart(part);
            var drawing = new W.Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = 990000L, Cy = 792000L },
                    new DW.DocProperties { Id = (uint)(main.ImageParts.Count() + 1), Name = "Embedded image" },
                    new A.Graphic(new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = "Encoded image" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(new A.Blip { Embed = id }, new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = 990000L, Cy = 792000L }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
            Live.Append(new W.Run(drawing));
            return _owner.WrapImage(drawing, part);
        }
        catch
        {
            main.DeletePart(part);
            throw;
        }
    }

    /// <summary>Removes this paragraph and invalidates its retained wrappers.</summary>
    public void Remove() => Live.Remove();
}

/// <summary>An editable text run.</summary>
[ObjoExport]
public sealed class Run
{
    private readonly Document _owner;
    private readonly W.Run _element;
    internal Run(Document owner, W.Run element) { _owner = owner; _element = element; }
    private W.Run Live { get { _owner.RequireAttached(_element); return _element; } }

    /// <summary>The run's text.</summary>
    public string Text
    {
        get => Live.GetFirstChild<W.Text>()?.Text ?? "";
        set
        {
            var text = Live.GetFirstChild<W.Text>();
            if (text is null) Live.Append(new W.Text(value) { Space = SpaceProcessingModeValues.Preserve });
            else { text.Text = value; text.Space = SpaceProcessingModeValues.Preserve; }
        }
    }

    /// <summary>Whether the run is bold.</summary>
    public bool Bold
    {
        get => Live.RunProperties?.Bold is not null;
        set { Live.RunProperties ??= new W.RunProperties(); Live.RunProperties.Bold = value ? new W.Bold() : null; }
    }

    /// <summary>Whether the run is italic.</summary>
    public bool Italic
    {
        get => Live.RunProperties?.Italic is not null;
        set { Live.RunProperties ??= new W.RunProperties(); Live.RunProperties.Italic = value ? new W.Italic() : null; }
    }

    /// <summary>Six-digit RGB text colour; empty string means automatic.</summary>
    public string ColourHex
    {
        get => Live.RunProperties?.Color?.Val?.Value ?? "";
        set
        {
            if (value.Length != 0 && (value.Length != 6 || !value.All(Uri.IsHexDigit)))
                throw new ArgumentException("Colour must be six hexadecimal digits.", nameof(value));
            Live.RunProperties ??= new W.RunProperties();
            Live.RunProperties.Color = value.Length == 0 ? null : new W.Color { Val = value.ToUpperInvariant() };
        }
    }

    /// <summary>Removes this run.</summary>
    public void Remove() => Live.Remove();
}

/// <summary>A table containing ordered rows.</summary>
[ObjoExport]
public sealed class Table
{
    private readonly Document _owner;
    private readonly W.Table _element;
    internal Table(Document owner, W.Table element) { _owner = owner; _element = element; }
    private W.Table Live { get { _owner.RequireAttached(_element); return _element; } }

    /// <summary>Appends an empty row.</summary>
    public Row AddRow()
    {
        var element = new W.TableRow();
        Live.Append(element);
        return _owner.WrapRow(element);
    }

    /// <summary>Snapshot of rows.</summary>
    public Row[] Rows() => Live.Elements<W.TableRow>().Select(_owner.WrapRow).ToArray();

    /// <summary>Number of rows.</summary>
    public long Count => Rows().LongLength;

    /// <summary>Row by zero-based index.</summary>
    public Row Item(long index)
    {
        var rows = Rows();
        return index < 0 || index >= rows.LongLength ? throw new IndexOutOfRangeException() : rows[index];
    }

    /// <summary>Removes the table.</summary>
    public void Remove() => Live.Remove();
}

/// <summary>A table row containing ordered cells.</summary>
[ObjoExport]
public sealed class Row
{
    private readonly Document _owner;
    private readonly W.TableRow _element;
    internal Row(Document owner, W.TableRow element) { _owner = owner; _element = element; }
    private W.TableRow Live { get { _owner.RequireAttached(_element); return _element; } }

    /// <summary>Appends a cell with an initially empty paragraph.</summary>
    public Cell AddCell()
    {
        var element = new W.TableCell(new W.Paragraph());
        Live.Append(element);
        var table = (W.Table)Live.Parent!;
        var count = Live.Elements<W.TableCell>().Count();
        var grid = table.GetFirstChild<W.TableGrid>()!;
        while (grid.Elements<W.GridColumn>().Count() < count)
            grid.Append(new W.GridColumn());
        return _owner.WrapCell(element);
    }

    /// <summary>Snapshot of cells.</summary>
    public Cell[] Cells() => Live.Elements<W.TableCell>().Select(_owner.WrapCell).ToArray();

    /// <summary>Number of cells.</summary>
    public long Count => Cells().LongLength;

    /// <summary>Cell by zero-based index.</summary>
    public Cell Item(long index)
    {
        var cells = Cells();
        return index < 0 || index >= cells.LongLength ? throw new IndexOutOfRangeException() : cells[index];
    }
}

/// <summary>A table cell that can contain paragraphs and nested tables.</summary>
[ObjoExport]
public sealed class Cell
{
    private readonly Document _owner;
    private readonly W.TableCell _element;
    internal Cell(Document owner, W.TableCell element) { _owner = owner; _element = element; }
    private W.TableCell Live { get { _owner.RequireAttached(_element); return _element; } }

    /// <summary>Appends a paragraph.</summary>
    public Paragraph AddParagraph(string text)
    {
        var element = new W.Paragraph(new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        Live.Append(element);
        return _owner.WrapParagraph(element);
    }

    /// <summary>Appends a nested table.</summary>
    public Table AddTable()
    {
        var element = new W.Table(new W.TableProperties(), new W.TableGrid(new W.GridColumn()));
        Live.Append(element);
        Live.Append(new W.Paragraph());
        return _owner.WrapTable(element);
    }

    /// <summary>Snapshot of direct paragraphs.</summary>
    public Paragraph[] Paragraphs() => Live.Elements<W.Paragraph>().Select(_owner.WrapParagraph).ToArray();

    /// <summary>Snapshot of direct nested tables.</summary>
    public Table[] Tables() => Live.Elements<W.Table>().Select(_owner.WrapTable).ToArray();
}

/// <summary>A named paragraph style owned by a document.</summary>
[ObjoExport]
public sealed class Style
{
    private readonly Document _owner;
    private readonly W.Style _element;
    internal Style(Document owner, W.Style element) { _owner = owner; _element = element; }

    /// <summary>Style identifier.</summary>
    public string Id { get { _owner.RequireAttached(_element); return _element.StyleId?.Value ?? ""; } }

    /// <summary>Display name.</summary>
    public string Name { get { _owner.RequireAttached(_element); return _element.StyleName?.Val?.Value ?? ""; } }
}

/// <summary>A section header or footer.</summary>
[ObjoExport]
public sealed class HeaderFooter
{
    private readonly Document _owner;
    private readonly OpenXmlCompositeElement _element;
    private readonly bool _header;
    internal HeaderFooter(Document owner, OpenXmlCompositeElement element, bool header)
    { _owner = owner; _element = element; _header = header; }

    /// <summary>True for a header, false for a footer.</summary>
    public bool IsHeader { get { _owner.RequireLive(); return _header; } }

    /// <summary>Concatenated header or footer text.</summary>
    public string Text { get { _owner.RequireLive(); return string.Concat(_element.Descendants<W.Text>().Select(text => text.Text)); } }

    /// <summary>Appends a paragraph.</summary>
    public Paragraph AddParagraph(string text)
    {
        _owner.RequireLive();
        var element = new W.Paragraph(new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        _element.Append(element);
        return _owner.WrapParagraph(element);
    }
}

/// <summary>Encoded inline image data owned by a document.</summary>
[ObjoExport]
public sealed class Image
{
    private readonly Document _owner;
    private readonly W.Drawing _drawing;
    private readonly ImagePart _part;
    internal Image(Document owner, W.Drawing drawing, ImagePart part)
    { _owner = owner; _drawing = drawing; _part = part; }

    /// <summary>MIME type of the encoded image.</summary>
    public string ContentType { get { _owner.RequireAttached(_drawing); return _part.ContentType; } }

    /// <summary>Returns an independent copy of the encoded PNG or JPEG bytes.</summary>
    public byte[] Data()
    {
        _owner.RequireAttached(_drawing);
        using var stream = _part.GetStream();
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
