using System.Globalization;
using System.Text;

namespace LuSplit.Infrastructure.Export;

internal enum PdfLineStyle { Normal, Title, Heading, Muted, Separator, Empty }

internal sealed record PdfLine(string Text, PdfLineStyle Style = PdfLineStyle.Normal);

/// <summary>
/// Generates a minimal valid PDF/1.4 document (A4, Helvetica, Latin-1 text).
/// No external dependencies required.
/// </summary>
internal static class SimplePdfBuilder
{
    private const float W = 595f;     // A4 width  (pt)
    private const float H = 842f;     // A4 height (pt)
    private const float ML = 50f;     // left margin
    private const float MR = 50f;     // right margin
    private const float TopY = 790f;  // y of first line (origin is bottom-left)
    private const float BottomY = 60f; // minimum y before page break

    private static float FontSize(PdfLineStyle s) => s switch
    {
        PdfLineStyle.Title => 20f,
        PdfLineStyle.Heading => 14f,
        PdfLineStyle.Muted => 10f,
        _ => 11f
    };

    private static float LineH(PdfLineStyle s) => s switch
    {
        PdfLineStyle.Title => 30f,
        PdfLineStyle.Heading => 22f,
        PdfLineStyle.Muted => 14f,
        PdfLineStyle.Separator => 12f,
        PdfLineStyle.Empty => 8f,
        _ => 17f
    };

    private static bool IsBold(PdfLineStyle s) =>
        s is PdfLineStyle.Title or PdfLineStyle.Heading;

    private static int MaxChars(PdfLineStyle s)
    {
        var avg = FontSize(s) * 0.55f;
        return Math.Max(10, (int)((W - ML - MR) / avg));
    }

    public static byte[] Build(IReadOnlyList<PdfLine> lines)
    {
        var wrapped = Wrap(lines);
        var pages = Paginate(wrapped);
        return Render(pages);
    }

    private static List<PdfLine> Wrap(IReadOnlyList<PdfLine> lines)
    {
        var result = new List<PdfLine>();
        foreach (var line in lines)
        {
            if (line.Style is PdfLineStyle.Separator or PdfLineStyle.Empty)
            {
                result.Add(line);
                continue;
            }
            var max = MaxChars(line.Style);
            var rem = line.Text;
            while (rem.Length > max)
            {
                var cut = rem.LastIndexOf(' ', max);
                if (cut <= 0) cut = max;
                result.Add(line with { Text = rem[..cut].TrimEnd() });
                rem = rem[cut..].TrimStart();
            }
            result.Add(line with { Text = rem });
        }
        return result;
    }

    private static List<List<(PdfLine Line, float Y)>> Paginate(List<PdfLine> lines)
    {
        var pages = new List<List<(PdfLine, float)>>();
        var cur = new List<(PdfLine, float)>();
        float y = TopY;
        foreach (var line in lines)
        {
            var lh = LineH(line.Style);
            if (y - lh < BottomY && cur.Count > 0)
            {
                pages.Add(cur);
                cur = new List<(PdfLine, float)>();
                y = TopY;
            }
            cur.Add((line, y));
            y -= lh;
        }
        if (cur.Count > 0) pages.Add(cur);
        if (pages.Count == 0) pages.Add(new List<(PdfLine, float)>());
        return pages;
    }

    private static byte[] Render(List<List<(PdfLine, float)>> pages)
    {
        int np = pages.Count;
        int pageBase = 5;
        int contentBase = pageBase + np;
        int totalObjs = 4 + 2 * np;

        using var ms = new MemoryStream();
        var offsets = new long[totalObjs + 1]; // 1-indexed

        void A(string s) => ms.Write(Encoding.Latin1.GetBytes(s));
        void AB(byte[] b) => ms.Write(b);

        // PDF header
        A("%PDF-1.4\n");

        // Object 1: Document Catalog
        offsets[1] = ms.Position;
        A("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // Object 2: Pages tree
        var kids = string.Join(" ", Enumerable.Range(pageBase, np).Select(i => $"{i} 0 R"));
        offsets[2] = ms.Position;
        A($"2 0 obj\n<< /Type /Pages /Kids [{kids}] /Count {np} >>\nendobj\n");

        // Object 3: Font F1 (Helvetica regular)
        offsets[3] = ms.Position;
        A("3 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

        // Object 4: Font F2 (Helvetica bold)
        offsets[4] = ms.Position;
        A("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        // One Page + Content stream per page
        for (int p = 0; p < np; p++)
        {
            var cs = BuildContent(pages[p]);
            int pid = pageBase + p, cid = contentBase + p;

            offsets[pid] = ms.Position;
            A($"{pid} 0 obj\n");
            A($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842]\n");
            A($"   /Contents {cid} 0 R\n");
            A("   /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> >>\n");
            A("endobj\n");

            offsets[cid] = ms.Position;
            A($"{cid} 0 obj\n<< /Length {cs.Length} >>\nstream\n");
            AB(cs);
            A("\nendstream\nendobj\n");
        }

        // Cross-reference table (each entry exactly 20 bytes via \r\n)
        long xrefPos = ms.Position;
        A($"xref\n0 {totalObjs + 1}\n");
        A("0000000000 65535 f\r\n"); // free entry: 20 bytes
        for (int i = 1; i <= totalObjs; i++)
            A($"{offsets[i]:D10} 00000 n\r\n"); // 20 bytes each

        // Trailer
        A("trailer\n");
        A($"<< /Size {totalObjs + 1} /Root 1 0 R >>\n");
        A("startxref\n");
        A($"{xrefPos}\n");
        A("%%EOF\n");

        return ms.ToArray();
    }

    private static byte[] BuildContent(List<(PdfLine Line, float Y)> items)
    {
        using var ms = new MemoryStream();
        void A(string s) => ms.Write(Encoding.Latin1.GetBytes(s));

        // Draw separator rules first (grey horizontal lines)
        A("0.4 w\n0.65 0.65 0.65 RG\n");
        foreach (var (line, y) in items)
        {
            if (line.Style == PdfLineStyle.Separator)
            {
                var yr = Fp(y - 4f);
                A($"{Fp(ML)} {yr} m\n{Fp(W - MR)} {yr} l\nS\n");
            }
        }

        // Draw text — one BT/ET block per line for clean absolute positioning
        foreach (var (line, y) in items)
        {
            if (line.Style is PdfLineStyle.Empty or PdfLineStyle.Separator) continue;
            var font = IsBold(line.Style) ? "F2" : "F1";
            var sz = Fp(FontSize(line.Style));
            A($"BT\n/{font} {sz} Tf\n{Fp(ML)} {Fp(y)} Td\n({Encode(line.Text)}) Tj\nET\n");
        }

        return ms.ToArray();
    }

    // Compact float representation for PDF operators
    private static string Fp(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    // Encode text as a PDF literal string: escape (, ), \  and replace non-Latin-1 with ?
    private static string Encode(string text)
    {
        var sb = new StringBuilder(text.Length + 4);
        foreach (char c in text)
        {
            if (c == '(') sb.Append("\\(");
            else if (c == ')') sb.Append("\\)");
            else if (c == '\\') sb.Append("\\\\");
            else if (c is >= '\x20' and <= '\xFF') sb.Append(c);
            else sb.Append('?');
        }
        return sb.ToString();
    }
}
