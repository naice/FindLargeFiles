using System.Globalization;
using System.Net;
using System.Text;
using FindLargeFiles.Core;
using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.Reporting;

/// <summary>A single self-contained HTML file (no external resources) that works offline, in light and dark mode, and prints well.</summary>
public static class HtmlReportWriter
{
    public static void Write(StorageReport report, string path) =>
        File.WriteAllText(path, Render(report), new UTF8Encoding(false));

    public static string Render(StorageReport report)
    {
        var sb = new StringBuilder(64 * 1024);
        sb.Append("<!doctype html>\n<html lang=\"").Append(H(Loc.Current.Culture.TwoLetterISOLanguageName)).Append("\">\n<head>\n<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<meta name=\"generator\" content=\"FindLargeFiles\">\n");
        sb.Append("<title>").Append(H($"{report.Title} – {report.RootPath}")).Append("</title>\n<style>").Append(Css).Append("</style>\n</head>\n<body>\n<main>\n");

        // Header
        sb.Append("<header><p class=\"eyebrow\">").Append(H(report.Title)).Append("</p><h1>").Append(H(report.RootPath)).Append("</h1>");
        sb.Append("<p class=\"muted\">").Append(H(Loc.T("report.created", ("date", ReportStyle.LongDate(report.CreatedAt))))).Append("</p></header>\n");

        // KPI tiles
        sb.Append("<section class=\"tiles\">");
        Tile(sb, Loc.T("report.total_size"), ReportStyle.Bytes(report.TotalSize));
        Tile(sb, Loc.T("report.files"), ReportStyle.Number(report.FileCount));
        Tile(sb, Loc.T("report.folders"), ReportStyle.Number(report.FolderCount));
        if (report.Drive is { } drive)
            Tile(sb, Loc.T("report.drive", ("name", drive.Name)), Loc.T("report.drive_free", ("free", ReportStyle.Bytes(drive.FreeSpace))),
                Loc.T("report.drive_of_total_used", ("total", ReportStyle.Bytes(drive.TotalSize)),
                    ("used", ReportStyle.Percent((double)drive.UsedSpace / Math.Max(1, drive.TotalSize)))));
        if (report.InaccessibleFolders > 0)
            Tile(sb, Loc.T("report.inaccessible"), ReportStyle.Number(report.InaccessibleFolders), Loc.T("report.inaccessible_detail"));
        sb.Append("</section>\n");

        RenderBreakdown(sb, report);
        if (report.Tree.Count > 0) RenderTree(sb, report);
        if (report.TopFiles.Count > 0) RenderTopFiles(sb, report);
        if (report.Extensions.Count > 0) RenderExtensions(sb, report);
        if (report.Ages.Count > 0) RenderAges(sb, report);

        sb.Append("<footer class=\"muted\">").Append(H(Loc.T("report.footer_note"))).Append("</footer>\n");
        sb.Append("</main>\n<script>").Append(Script).Append("</script>\n</body>\n</html>\n");
        return sb.ToString();
    }

    private static void RenderBreakdown(StringBuilder sb, StorageReport report)
    {
        var slices = report.Breakdown;
        sb.Append("<section><h2>").Append(H(Loc.T("report.breakdown"))).Append("</h2><div class=\"split\">");
        if (slices.Count > 0)
        {
            const string hatch = "<pattern id=\"free\" width=\"8\" height=\"8\" patternUnits=\"userSpaceOnUse\" patternTransform=\"rotate(45)\">"
                                 + "<rect width=\"8\" height=\"8\" fill=\"var(--free)\"/><line x1=\"0\" y1=\"0\" x2=\"0\" y2=\"8\" stroke=\"var(--free-line)\" stroke-width=\"3\"/></pattern>";
            var svg = ReportStyle.DonutSvg(slices, i => Fill(slices, i), "var(--surface)", Loc.T("chart.center_used"),
                ReportStyle.Bytes(ReportStyle.UsedInBreakdown(report)), "var(--text)", "var(--muted)", 220, hatch);
            sb.Append("<figure class=\"donut\">").Append(svg).Append("</figure>");
        }

        sb.Append("<table class=\"legend\"><thead><tr><th>").Append(H(Loc.T("report.column_element")))
          .Append("</th><th class=\"num\">").Append(H(Loc.T("report.column_size")))
          .Append("</th><th class=\"num\">").Append(H(Loc.T("report.column_share"))).Append("</th></tr></thead><tbody>");
        for (var i = 0; i < slices.Count; i++)
        {
            var swatch = slices[i].Kind == SliceKind.FreeSpace
                ? "<span class=\"swatch free\"></span>"
                : $"<span class=\"swatch\" style=\"background:{Fill(slices, i)}\"></span>";
            sb.Append("<tr><td>").Append(swatch)
              .Append(H(slices[i].Label)).Append("</td><td class=\"num\">").Append(H(ReportStyle.Bytes(slices[i].Size)))
              .Append("</td><td class=\"num\">").Append(H(ReportStyle.Percent(slices[i].Share))).Append("</td></tr>");
        }

        sb.Append("</tbody></table></div></section>\n");
    }

    private static readonly string[] SeriesVars =
        Enumerable.Range(1, ReportStyle.LightSeries.Length).Select(n => $"var(--s{n})").ToArray();

    private static string Fill(IReadOnlyList<ReportSlice> slices, int i) =>
        ReportStyle.SliceColor(slices, i, SeriesVars, "var(--other)", "url(#free)");

    private static void RenderTree(StringBuilder sb, StorageReport report)
    {
        var o = report.Options;
        sb.Append("<section><h2>").Append(H(Loc.T("report.structure"))).Append("</h2><p class=\"muted\">")
          .Append(H(Loc.T("report.structure_note", ("depth", o.TreeDepth), ("share", ReportStyle.Percent(o.MinShare)))))
          .Append(" <button type=\"button\" class=\"link\" data-tree=\"expand\">").Append(H(Loc.T("report.expand_all")))
          .Append("</button> · <button type=\"button\" class=\"link\" data-tree=\"collapse\">").Append(H(Loc.T("report.collapse_all"))).Append("</button></p>");
        sb.Append("<div class=\"scroll\"><table class=\"tree\"><thead><tr>")
          .Append("<th>").Append(H(Loc.T("report.column_name"))).Append("</th>")
          .Append("<th class=\"num\">").Append(H(Loc.T("report.column_size"))).Append("</th>")
          .Append("<th class=\"bar-col\">").Append(H(Loc.T("report.column_share_of_folder"))).Append("</th>")
          .Append("<th class=\"num\">").Append(H(Loc.T("report.column_files"))).Append("</th>")
          .Append("<th class=\"num\">").Append(H(Loc.T("report.column_folders"))).Append("</th>")
          .Append("<th>").Append(H(Loc.T("report.column_modified"))).Append("</th></tr></thead><tbody>");

        var tree = report.Tree;
        for (var i = 0; i < tree.Count; i++)
        {
            var e = tree[i];
            var hasChildren = i + 1 < tree.Count && tree[i + 1].Depth > e.Depth;
            var cls = e.IsSummary ? "summary" : e.IsDirectory ? "dir" : "file";
            sb.Append(CultureInfo.InvariantCulture, $"<tr class=\"{cls}\" data-depth=\"{e.Depth}\"");
            if (e.Depth > 1) sb.Append(" hidden");
            sb.Append(CultureInfo.InvariantCulture, $"><td style=\"--depth:{e.Depth}\" title=\"{H(e.Path)}\">");
            if (hasChildren)
                sb.Append("<button type=\"button\" class=\"toggle\" aria-expanded=\"").Append(e.Depth == 0 ? "true" : "false").Append("\" aria-label=\"").Append(H(Loc.T("report.expand"))).Append("\"></button>");
            else
                sb.Append("<span class=\"toggle-space\"></span>");
            sb.Append("<span class=\"icon\" aria-hidden=\"true\"></span>").Append(H(e.Name));
            if (e.IsInaccessible) sb.Append(" <span class=\"badge\">").Append(H(Loc.T("report.no_access_badge"))).Append("</span>");
            sb.Append("</td>");
            sb.Append("<td class=\"num strong\">").Append(H(ReportStyle.Bytes(e.Size))).Append("</td>");
            Bar(sb, e.ShareOfParent);
            sb.Append("<td class=\"num\">").Append(e.IsSummary ? "" : H(ReportStyle.Number(e.FileCount))).Append("</td>");
            sb.Append("<td class=\"num\">").Append(e.IsSummary || !e.IsDirectory ? "" : H(ReportStyle.Number(e.FolderCount))).Append("</td>");
            sb.Append("<td class=\"date\">").Append(H(ReportStyle.Date(e.Modified))).Append("</td></tr>");
        }

        sb.Append("</tbody></table></div></section>\n");
    }

    private static void RenderTopFiles(StringBuilder sb, StorageReport report)
    {
        sb.Append("<section><h2>").Append(H(Loc.T("report.top_files", ("count", report.TopFiles.Count)))).Append("</h2><div class=\"scroll\"><table class=\"sortable\"><thead><tr>");
        sb.Append("<th class=\"num\" data-type=\"num\">#</th>")
          .Append("<th>").Append(H(Loc.T("report.column_file"))).Append("</th>")
          .Append("<th class=\"num\" data-type=\"num\">").Append(H(Loc.T("report.column_size"))).Append("</th>")
          .Append("<th data-type=\"date\">").Append(H(Loc.T("report.column_modified"))).Append("</th>")
          .Append("<th>").Append(H(Loc.T("report.column_folder"))).Append("</th></tr></thead><tbody>");
        foreach (var f in report.TopFiles)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<tr><td class=\"num muted\" data-v=\"{f.Rank}\">{f.Rank}</td><td>{H(f.Name)}</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num strong\" data-v=\"{f.Size}\">{H(ReportStyle.Bytes(f.Size))}</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"date\" data-v=\"{f.Modified?.Ticks ?? 0}\">{H(ReportStyle.Date(f.Modified))}</td>");
            sb.Append("<td class=\"path\">").Append(H(f.Folder)).Append("</td></tr>");
        }

        sb.Append("</tbody></table></div></section>\n");
    }

    private static void RenderExtensions(StringBuilder sb, StorageReport report)
    {
        sb.Append("<section><h2>").Append(H(Loc.T("report.file_types"))).Append("</h2><div class=\"scroll\"><table class=\"sortable\"><thead><tr>");
        sb.Append("<th>").Append(H(Loc.T("report.column_type"))).Append("</th>")
          .Append("<th class=\"num\" data-type=\"num\">").Append(H(Loc.T("report.column_files"))).Append("</th>")
          .Append("<th class=\"num\" data-type=\"num\">").Append(H(Loc.T("report.column_size"))).Append("</th>")
          .Append("<th class=\"bar-col\" data-type=\"num\">").Append(H(Loc.T("report.column_share"))).Append("</th></tr></thead><tbody>");
        foreach (var e in report.Extensions)
        {
            sb.Append("<tr><td class=\"mono\">").Append(H(e.Extension)).Append("</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num\" data-v=\"{e.FileCount}\">{H(ReportStyle.Number(e.FileCount))}</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num strong\" data-v=\"{e.Size}\">{H(ReportStyle.Bytes(e.Size))}</td>");
            Bar(sb, e.Share);
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table></div></section>\n");
    }

    private static void RenderAges(StringBuilder sb, StorageReport report)
    {
        var max = Math.Max(1, report.Ages.Max(a => a.Size));
        sb.Append("<section><h2>").Append(H(Loc.T("report.file_age"))).Append("</h2><p class=\"muted\">").Append(H(Loc.T("report.file_age_note"))).Append("</p><div class=\"ages\">");
        foreach (var a in report.Ages)
        {
            sb.Append("<div class=\"age\"><span>").Append(H(a.Label)).Append("</span>");
            sb.Append(CultureInfo.InvariantCulture, $"<span class=\"age-track\"><span class=\"age-bar\" style=\"width:{100.0 * a.Size / max:0.##}%\"></span></span>");
            sb.Append("<span class=\"num\"><b>").Append(H(ReportStyle.Bytes(a.Size))).Append("</b> ").Append(H(ReportStyle.Percent(a.Share)))
              .Append("<br><small class=\"muted\">").Append(H(Loc.Plural("global.file_count", a.FileCount))).Append("</small></span></div>");
        }

        sb.Append("</div></section>\n");
    }

    private static void Tile(StringBuilder sb, string label, string value, string? detail = null)
    {
        sb.Append("<div class=\"tile\"><span class=\"label\">").Append(H(label)).Append("</span><span class=\"value\">").Append(H(value)).Append("</span>");
        if (detail != null) sb.Append("<span class=\"muted small\">").Append(H(detail)).Append("</span>");
        sb.Append("</div>");
    }

    private static void Bar(StringBuilder sb, double share)
    {
        sb.Append(CultureInfo.InvariantCulture,
            $"<td class=\"bar-col\" data-v=\"{share:0.######}\"><span class=\"bar\"><span style=\"width:{Math.Clamp(share, 0, 1) * 100:0.##}%\"></span></span><span class=\"pct\">{H(ReportStyle.Percent(share))}</span></td>");
    }

    private static string H(string value) => WebUtility.HtmlEncode(value);

    private const string Css = """

:root{color-scheme:light;--bg:#f6f6f3;--surface:#ffffff;--border:#e3e2dc;--text:#1b1b19;--muted:#66655f;--hover:#f0efea;--data:#2a78d6;--track:#ecebe6;
--s1:#2a78d6;--s2:#eb6834;--s3:#1baf7a;--s4:#eda100;--s5:#e87ba4;--s6:#008300;--s7:#4a3aa7;--s8:#e34948;--other:#9a9890;--free:#ecebe6;--free-line:#d6d4cc;--warn:#b25e00}
@media (prefers-color-scheme:dark){:root{color-scheme:dark;--bg:#181b24;--surface:#212534;--border:#2e3445;--text:#e7e9f0;--muted:#a4abbc;--hover:#2b3043;--data:#3987e5;--track:#2c3244;
--s1:#3987e5;--s2:#d95926;--s3:#199e70;--s4:#c98500;--s5:#d55181;--s6:#008300;--s7:#9085e9;--s8:#e66767;--other:#5f667a;--free:#2e3446;--free-line:#4a5268;--warn:#e0a030}}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--text);font:14px/1.45 "Segoe UI Variable Text","Segoe UI","Nirmala UI",system-ui,sans-serif}
main{max-width:1180px;margin:0 auto;padding:32px 16px 48px}
header{margin-bottom:24px}
h1{font-size:26px;margin:2px 0 4px;word-break:break-all}
h2{font-size:17px;margin:0 0 12px}
.eyebrow{margin:0;text-transform:uppercase;letter-spacing:.08em;font-size:12px;font-weight:600;color:var(--muted)}
.muted{color:var(--muted)}.small{font-size:12px}.strong{font-weight:600}.mono{font-family:"Cascadia Mono",Consolas,monospace;font-size:13px}
section{background:var(--surface);border:1px solid var(--border);border-radius:12px;padding:20px;margin-bottom:16px}
.tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px;background:none;border:0;padding:0}
.tile{background:var(--surface);border:1px solid var(--border);border-radius:12px;padding:14px 16px;display:flex;flex-direction:column;gap:2px}
.tile .label{font-size:12px;color:var(--muted)}.tile .value{font-size:22px;font-weight:600}
.split{display:flex;gap:32px;align-items:center;flex-wrap:wrap}
.donut{margin:0;flex:0 0 auto}.donut svg{display:block;max-width:100%;height:auto}
.legend{flex:1 1 320px}
table{width:100%;border-collapse:collapse}
th{font-size:12px;font-weight:600;color:var(--muted);text-align:left;padding:8px 10px;border-bottom:1px solid var(--border);white-space:nowrap}
td{padding:6px 10px;border-bottom:1px solid var(--border);vertical-align:middle}
tbody tr:hover{background:var(--hover)}
.num{text-align:right;font-variant-numeric:tabular-nums;white-space:nowrap}
td.date{white-space:nowrap}
.path{color:var(--muted);font-size:12.5px;word-break:break-all}
.swatch{display:inline-block;width:10px;height:10px;border-radius:3px;margin-right:10px;vertical-align:-1px}
.swatch.free{background:repeating-linear-gradient(45deg,var(--free-line) 0 2px,var(--free) 2px 5px)}
.scroll{overflow-x:auto}
.bar-col{min-width:170px;white-space:nowrap}
.bar{display:inline-block;width:110px;height:6px;border-radius:3px;background:var(--track);vertical-align:middle;overflow:hidden}
.bar>span{display:block;height:100%;background:var(--data);border-radius:0 3px 3px 0;min-width:2px}
.pct{display:inline-block;width:56px;text-align:right;color:var(--muted);font-size:12.5px;font-variant-numeric:tabular-nums}
.tree td:first-child{padding-left:calc(10px + var(--depth) * 20px);white-space:nowrap;max-width:460px;overflow:hidden;text-overflow:ellipsis}
.toggle,.toggle-space{display:inline-block;width:20px;height:20px;margin-right:2px;vertical-align:middle}
.toggle{border:0;background:none;color:var(--muted);cursor:pointer;border-radius:4px;padding:0}
.toggle:hover{background:var(--hover);color:var(--text)}
.toggle::before{content:"";display:inline-block;width:6px;height:6px;border-right:1.5px solid currentColor;border-bottom:1.5px solid currentColor;transform:rotate(-45deg);transition:transform .15s}
.toggle[aria-expanded=true]::before{transform:rotate(45deg)}
.icon{display:inline-block;width:14px;height:11px;margin-right:8px;vertical-align:-1px;border-radius:2px}
tr.dir .icon{background:#d9a441}
tr.file .icon{width:10px;height:13px;border:1.5px solid var(--muted);border-radius:2px;margin:0 10px 0 2px}
tr.summary td{color:var(--muted);font-style:italic}tr.summary .icon{display:none}
.badge{font-size:11px;color:var(--warn);border:1px solid currentColor;border-radius:4px;padding:0 5px;margin-left:6px}
.link{border:0;background:none;color:var(--data);cursor:pointer;padding:0;font:inherit}
.sortable th{cursor:pointer;user-select:none}.sortable th:hover{color:var(--text)}
.sortable th[aria-sort=ascending]::after{content:" ↑"}.sortable th[aria-sort=descending]::after{content:" ↓"}
.ages{display:flex;flex-direction:column;gap:10px}
.age{display:grid;grid-template-columns:110px 1fr 150px;gap:12px;align-items:center}
.age-track{height:14px}.age-bar{display:block;height:100%;background:var(--data);border-radius:0 4px 4px 0;min-width:2px}
footer{margin-top:24px;font-size:12px;text-align:center}
@media (max-width:640px){.age{grid-template-columns:80px 1fr 110px}h1{font-size:20px}}
@media print{body{background:#fff}section,.tile{break-inside:avoid;border-color:#ccc}.toggle,.link{display:none}tr[hidden]{display:table-row}}

""";

    private const string Script = """

(function(){
  var rows = Array.prototype.slice.call(document.querySelectorAll('table.tree tbody tr'));
  function depth(r){ return +r.getAttribute('data-depth'); }
  function setOpen(i, open){
    var btn = rows[i].querySelector('.toggle'); if (btn) btn.setAttribute('aria-expanded', open);
    var d = depth(rows[i]);
    for (var j = i + 1; j < rows.length && depth(rows[j]) > d; j++){
      if (!open){ rows[j].hidden = true; continue; }
      if (depth(rows[j]) === d + 1){
        rows[j].hidden = false;
        var b = rows[j].querySelector('.toggle');
        if (b && b.getAttribute('aria-expanded') === 'true') setOpen(j, true);
      }
    }
  }
  rows.forEach(function(r, i){
    var btn = r.querySelector('.toggle');
    if (btn) btn.addEventListener('click', function(){ setOpen(i, btn.getAttribute('aria-expanded') !== 'true'); });
  });
  document.querySelectorAll('[data-tree]').forEach(function(b){
    b.addEventListener('click', function(){
      var open = b.getAttribute('data-tree') === 'expand';
      rows.forEach(function(r){
        var t = r.querySelector('.toggle'), d = depth(r);
        if (t) t.setAttribute('aria-expanded', open || d === 0);
        r.hidden = !open && d > 1;
      });
    });
  });
  document.querySelectorAll('table.sortable').forEach(function(table){
    var heads = table.querySelectorAll('th');
    heads.forEach(function(th, col){
      th.addEventListener('click', function(){
        var asc = th.getAttribute('aria-sort') !== 'ascending';
        heads.forEach(function(h){ h.removeAttribute('aria-sort'); });
        th.setAttribute('aria-sort', asc ? 'ascending' : 'descending');
        var body = table.tBodies[0], list = Array.prototype.slice.call(body.rows);
        var numeric = th.hasAttribute('data-type');
        list.sort(function(a, b){
          var x = a.cells[col], y = b.cells[col];
          var r = numeric ? (+(x.getAttribute('data-v')) - +(y.getAttribute('data-v'))) : x.textContent.localeCompare(y.textContent, 'de');
          return asc ? r : -r;
        });
        list.forEach(function(r){ body.appendChild(r); });
      });
    });
  });
})();

""";
}
