// =====================================================================
//  JAMES BUILDS — static page generator
//
//  Why this exists:
//  The site draws builds with JavaScript, but search engines and AI
//  crawlers often don't run JavaScript. This script reads
//  data/builds.json and writes the build info straight into the HTML,
//  so bots see the same content people do.
//
//  How to run it (from the website folder, after editing builds.json):
//      dotnet run tools/generate.cs
//
//  What it writes:
//    builds/<id>/index.html   one real page per build (template: build.html)
//    builds/index.html        small redirect to builds.html
//    builds.html              build cards between the GENERATED markers
//    index.html               hero card + carousel between the markers
//    sitemap.xml              every page, including each build page
//
//  The JavaScript still runs on top and redraws the same content,
//  so filters, the gallery and the enquiry form keep working.
// =====================================================================

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

const string SiteUrl = "https://www.jamesbuilds.tech";

string root = Directory.GetCurrentDirectory();
string jsonPath = Path.Combine(root, "data", "builds.json");
if (!File.Exists(jsonPath))
{
    Console.Error.WriteLine("Can't find data/builds.json. Run this from the website folder:  dotnet run tools/generate.cs");
    return 1;
}

// JsonObject keeps keys in file order, so specs show in the order you wrote them.
List<JsonObject> builds = JsonNode.Parse(File.ReadAllText(jsonPath))!.AsArray()
    .Select(node => node!.AsObject())
    .ToList();

var changed = new List<string>();

// ── 1. One page per build ────────────────────────────────────────────
string template = File.ReadAllText(Path.Combine(root, "build.html"));
string buildsDir = Path.Combine(root, "builds");
Directory.CreateDirectory(buildsDir);

foreach (var build in builds)
{
    string id = Text(build, "id");
    if (!Regex.IsMatch(id, "^[a-z0-9-]+$"))
    {
        Console.Error.WriteLine($"Build id '{id}' must be lowercase letters, numbers and dashes only (it becomes the web address).");
        return 1;
    }
    WriteIfChanged(Path.Combine(buildsDir, id, "index.html"), BuildPage(template, build, builds));
}

// Remove pages for builds that were deleted from builds.json
var ids = builds.Select(b => Text(b, "id")).ToHashSet();
foreach (var dir in Directory.GetDirectories(buildsDir))
{
    if (!ids.Contains(Path.GetFileName(dir)))
    {
        Directory.Delete(dir, recursive: true);
        changed.Add($"removed builds/{Path.GetFileName(dir)}/");
    }
}

// /builds/ on its own should land on the main builds list
WriteIfChanged(Path.Combine(buildsDir, "index.html"), $"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="UTF-8">
      <title>Builds — James Builds</title>
      <link rel="canonical" href="{SiteUrl}/builds.html">
      <meta http-equiv="refresh" content="0; url=/builds.html">
    </head>
    <body><a href="/builds.html">See all builds</a></body>
    </html>
    """);

// ── 2. Build cards on builds.html (same order as builds.js) ─────────
var statusOrder = new Dictionary<string, int> { ["In-Progress"] = 0, ["Available"] = 1, ["Reserved"] = 2, ["Archived"] = 3, ["Sold"] = 4 };
var sorted = builds.OrderBy(b => statusOrder.GetValueOrDefault(Text(b, "status"), 99)).ToList();
UpdateFile("builds.html", html => ReplaceBetween(html, "BUILD-CARDS", "\n" + string.Concat(sorted.Select(MiniCard)) + "        "));

// ── 3. Home page hero card + carousel (same rules as main.js) ───────
var homeBuilds = builds.Where(b => Flag(b, "homeFeatured")).Take(3).ToList();
if (homeBuilds.Count == 0) homeBuilds = builds.Where(b => Flag(b, "featured")).Take(3).ToList();
var heroBuild = builds.FirstOrDefault(b => Flag(b, "homeFeatured") && IsForSale(b));

UpdateFile("index.html", html =>
{
    html = ReplaceBetween(html, "HOME-SLIDES", string.Concat(homeBuilds.Select((b, i) => HomeSlide(b, i))));
    return ReplaceBetween(html, "HERO-CARD", "\n" + HeroCard(heroBuild) + "              ");
});

// ── 4. Sitemap ───────────────────────────────────────────────────────
string[] pages = ["", "builds.html", "services.html", "business-buyback.html", "blog.html", "contact.html"];
var sitemap = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
foreach (var page in pages) sitemap.Append($"  <url><loc>{SiteUrl}/{page}</loc></url>\n");
foreach (var build in builds) sitemap.Append($"  <url><loc>{SiteUrl}/builds/{Text(build, "id")}/</loc></url>\n");
sitemap.Append("</urlset>\n");
WriteIfChanged(Path.Combine(root, "sitemap.xml"), sitemap.ToString());

// ── 5. llms.txt — plain-text summary some AI tools read (llmstxt.org) ─
string BuildLine(JsonObject b) =>
    $"- [{Text(b, "name")}]({SiteUrl}/builds/{Text(b, "id")}/): {Price(b["price"])}, {Text(b, "status")}. {Text(b, "tagline").Trim()}";
var forSale = builds.Where(IsForSale).ToList();
WriteIfChanged(Path.Combine(root, "llms.txt"), $"""
    # James Builds

    > Custom gaming and workstation PCs built and stress tested by James in Docklands, Melbourne, Australia. Also PC repairs, hardware upgrades, custom water cooling and bulk buyback of business IT equipment. Pickup in Docklands. ABN 28 809 354 629.

    ## Builds for sale
    {(forSale.Count == 0 ? "- Nothing listed right now. Custom builds to order: " + SiteUrl + "/contact.html" : string.Join("\n", forSale.Select(BuildLine)))}

    ## Pages
    - [All builds, including sold ones]({SiteUrl}/builds.html)
    - [Services and prices]({SiteUrl}/services.html)
    - [Business bulk buyback]({SiteUrl}/business-buyback.html)
    - [Build log]({SiteUrl}/blog.html)
    - [Contact and quotes]({SiteUrl}/contact.html)

    ## Past builds
    {string.Join("\n", builds.Where(b => !IsForSale(b)).Select(BuildLine))}

    """);

Console.WriteLine(changed.Count == 0
    ? $"Everything already up to date ({builds.Count} builds)."
    : $"Updated {changed.Count} file(s) for {builds.Count} builds:\n  " + string.Join("\n  ", changed));
return 0;


// =====================================================================
//  Page pieces. Each one copies the matching JavaScript function so
//  the pre-filled HTML looks the same as what the browser draws.
// =====================================================================

// Full build page: build.html with the build's details written in.
string BuildPage(string html, JsonObject b, List<JsonObject> all)
{
    string id = Text(b, "id"), name = Text(b, "name"), status = Text(b, "status");
    bool isSold = status == "Sold";
    string url = $"{SiteUrl}/builds/{id}/";
    string title = $"{name} | {Price(b["price"])}{(isSold ? " (Sold)" : "")} | James Builds, Melbourne";
    string description = Text(b, "tagline").Trim();
    if (description.Length < 120) description += " Tested and ready for pickup in Docklands, Melbourne.";

    // <head>: title, description, canonical, social preview, structured data.
    // <base href="/"> makes the template's relative links (styles.css,
    // images/..., data/builds.json) work from inside /builds/<id>/.
    html = MustReplace(html, "<title id=\"pageTitle\">Build — James Builds</title>", $"<title id=\"pageTitle\">{Esc(title)}</title>");
    html = MustReplace(html, "content=\"Custom PC build by James Builds, Docklands Melbourne.\"", $"content=\"{Esc(description)}\"");
    string image = Text(b, "featuredImage");
    html = MustReplace(html, "<meta charset=\"UTF-8\">", $"""
        <meta charset="UTF-8">
          <base href="/">
          <link rel="canonical" href="{url}">
          <meta property="og:title" content="{Esc(name)}">
          <meta property="og:description" content="{Esc(description)}">
          <meta property="og:type" content="product">
          <meta property="og:url" content="{url}">
        {(image == "" ? "" : $"  <meta property=\"og:image\" content=\"{SiteUrl}/{image.TrimStart('/')}\">\n")}  <script type="application/ld+json">
        {StructuredData(b, url)}
          </script>
        """);

    // builds.js reads the id from here when there's no ?id= in the address
    html = MustReplace(html, "<body>", $"<body data-build-id=\"{id}\">");

    // Show the content straight away instead of the loading spinner
    html = Regex.Replace(html, "<div id=\"loadingState\" style=\"[^\"]*\">", "<div id=\"loadingState\" style=\"display:none;\">");
    html = MustReplace(html, "<div id=\"buildContent\" class=\"d-none\">", "<div id=\"buildContent\">");

    html = Fill(html, "breadcrumbName", Esc(name));
    html = image == ""
        ? MustReplace(html, "<img id=\"mainImage\" src=\"\" alt=\"Build main image\"", "<img id=\"mainImage\" src=\"\" alt=\"\" style=\"display:none;\"")
        : MustReplace(html, "<img id=\"mainImage\" src=\"\" alt=\"Build main image\"", $"<img id=\"mainImage\" src=\"{Esc(image)}\" alt=\"{Esc(name)}\"");
    html = Fill(html, "statusBadgeHero", $"<span class=\"tag {StatusClass(status)}\">{Esc(status)}</span>");

    var gallery = Strings(b, "gallery");
    if (gallery.Count > 1)
        html = Fill(html, "thumbStrip", string.Concat(gallery.Select((img, i) => $"""

                    <div class="build-thumb {(i == 0 ? "active" : "")}" onclick="switchImage('{Esc(img)}', this)">
                      <img src="{Esc(img)}" alt="{Esc(name)} photo {i + 1}" loading="lazy" onerror="this.parentElement.style.display='none'">
                    </div>
            """)));

    string categoryTags = string.Concat(Strings(b, "category").Select(c => $"<span class=\"tag {CategoryClass(c)}\">{Esc(c)}</span>"));
    html = Fill(html, "heroTags", (isSold ? "<span class=\"tag tag-sold\">SOLD</span>" : "") + categoryTags);
    html = Fill(html, "buildName", Esc(name));
    html = Fill(html, "buildTagline", Esc(Text(b, "tagline")));
    html = Fill(html, "priceBlock", PriceHtml(b, "build-page-price"));

    var specs = Pairs(b, "specs");
    html = Fill(html, "quickSpecs", string.Concat(specs.Take(5).Select(s =>
        $"<div class=\"quick-spec-row\"><span class=\"quick-spec-key\">{SpecIcon(s.Key, "")} {Esc(s.Key)}</span><span class=\"quick-spec-val\">{Esc(s.Value)}</span></div>")));

    string primary = Text(b["cta"]?.AsObject(), "primary", "Enquire About This Build");
    string secondary = Text(b["cta"]?.AsObject(), "secondary", "Reserve This Build");
    html = Fill(html, "ctaButtons", isSold
        ? """
            <div class="sold-notice mb-3">
              <span class="tag tag-sold">SOLD</span>
              <p style="margin-top:10px;color:var(--text-muted);">This build has already been sold. If you would like something similar, feel free to enquire below.</p>
            </div>
            <a href="#enquiryForm" class="btn-primary-custom w-100" onclick="scrollToForm(event)">Request Similar Build <i class="bi bi-arrow-right"></i></a>
          """
        : $"""
            <a href="#enquiryForm" class="btn-primary-custom" onclick="scrollToForm(event)">{Esc(primary)} <i class="bi bi-arrow-right"></i></a>
            <a href="#enquiryForm" class="btn-ghost-custom" onclick="scrollToForm(event, '{Esc(secondary)}')">{Esc(secondary)}</a>
          """);

    html = Fill(html, "specsTable", string.Concat(specs.Select(s => $"""

                <div class="spec-row">
                  <span class="spec-key">{SpecIcon(s.Key, "")} {Esc(s.Key)}</span>
                  <span class="spec-val">{Esc(s.Value)}</span>
                </div>
        """)));
    html = Fill(html, "buildDescription", Esc(Text(b, "description")));
    html = Fill(html, "highlightsList", string.Concat(Strings(b, "highlights").Select(h =>
        $"<div class=\"highlight-item\"><i class=\"bi bi-check-circle-fill\"></i> {Esc(h)}</div>")));

    var benchmarks = Pairs(b, "benchmarks");
    if (benchmarks.Count == 0)
        html = MustReplace(html, "<section id=\"benchmarkSection\" class=\"section-sm\" style=\"", "<section id=\"benchmarkSection\" class=\"section-sm\" style=\"display:none;");
    else
        html = Fill(html, "benchmarkCards", string.Concat(benchmarks.Select(s => $"""

                <div class="col-sm-6 col-lg-3">
                  <div class="benchmark-card">
                    <div class="benchmark-label">{Esc(s.Key)}</div>
                    <div class="benchmark-value">{Esc(s.Value)}</div>
                  </div>
                </div>
        """)));

    html = MustReplace(html, "id=\"formBuildName\">", $"id=\"formBuildName\" value=\"{Esc(name)}\">");
    html = MustReplace(html, "id=\"formBuildId\">", $"id=\"formBuildId\" value=\"{id}\">");
    html = MustReplace(html, "id=\"formBuildPrice\">", $"id=\"formBuildPrice\" value=\"{Price(b["price"])}\">");
    html = Fill(html, "enquiryBanner", $"""

          <div class="enquiry-build-banner">
            <div>
              <div style="font-size:var(--text-xs);color:var(--text-muted);text-transform:uppercase;letter-spacing:0.08em;margin-bottom:4px;">{(isSold ? "Previously Sold Build" : "Enquiring about")}</div>
              <div style="font-weight:700;font-family:'Space Grotesk',sans-serif;">{Esc(name)}</div>
            </div>
            <div style="display:flex;align-items:center;gap:10px;">
              {(isSold ? "<span class=\"tag tag-sold\">SOLD</span>" : "")}
              <div style="font-family:'JetBrains Mono',monospace;font-size:var(--text-lg);color:var(--text-primary);">{Price(b["price"])}</div>
            </div>
          </div>
        """);

    var others = all.Where(o => Text(o, "id") != id && Text(o, "status") != "Archived").Take(3);
    html = Fill(html, "otherBuilds", string.Concat(others.Select(MiniCard)));
    return html;
}

// Machine-readable product details (schema.org) for search engines and AI.
string StructuredData(JsonObject b, string url)
{
    string availability = Text(b, "status") switch
    {
        "Available" => "https://schema.org/InStock",
        "In-Progress" => "https://schema.org/PreOrder",
        "Reserved" => "https://schema.org/LimitedAvailability",
        _ => "https://schema.org/SoldOut",
    };
    var images = new JsonArray(Strings(b, "gallery").DefaultIfEmpty(Text(b, "featuredImage"))
        .Where(i => i != "").Select(i => (JsonNode)$"{SiteUrl}/{i.TrimStart('/')}").ToArray());
    var properties = new JsonArray(Pairs(b, "specs")
        .Select(s => (JsonNode)new JsonObject { ["@type"] = "PropertyValue", ["name"] = s.Key, ["value"] = s.Value.Trim() }).ToArray());

    var data = new JsonObject
    {
        ["@context"] = "https://schema.org",
        ["@graph"] = new JsonArray(
            new JsonObject
            {
                ["@type"] = "Product",
                ["@id"] = url + "#product",
                ["name"] = Text(b, "name"),
                ["description"] = Text(b, "description"),
                ["url"] = url,
                ["image"] = images,
                ["category"] = "Desktop gaming PC",
                ["brand"] = new JsonObject { ["@type"] = "Brand", ["name"] = "James Builds" },
                ["additionalProperty"] = properties,
                ["offers"] = new JsonObject
                {
                    ["@type"] = "Offer",
                    ["url"] = url,
                    ["price"] = b["price"]?.GetValue<decimal>(),
                    ["priceCurrency"] = "AUD",
                    ["availability"] = availability,
                    ["seller"] = new JsonObject { ["@id"] = SiteUrl + "/#business" },
                },
            },
            new JsonObject
            {
                ["@type"] = "BreadcrumbList",
                ["itemListElement"] = new JsonArray(
                    Crumb(1, "Home", SiteUrl + "/"),
                    Crumb(2, "Builds", SiteUrl + "/builds.html"),
                    Crumb(3, Text(b, "name"), url)),
            }),
    };
    // The default encoder escapes < > & so nothing can break out of the <script> tag
    return data.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
}

JsonObject Crumb(int position, string name, string url) =>
    new() { ["@type"] = "ListItem", ["position"] = position, ["name"] = name, ["item"] = url };

// Copy of buildMiniCard() in builds.js
string MiniCard(JsonObject b)
{
    string status = Text(b, "status");
    bool isSold = status == "Sold";
    var categories = Strings(b, "category");
    string filterTags = string.Join(" ", categories.Concat(status switch
    {
        "Available" => new[] { "available" },
        "Sold" => new[] { "sold" },
        "In-Progress" => new[] { "in-progress" },
        _ => Array.Empty<string>(),
    }));
    var specs = Pairs(b, "specs").Take(4);

    return $"""
                <div class="col-lg-4 col-md-6 build-col animate-fade-up">
                  <div class="build-card" data-category="{Esc(filterTags)}" {(isSold ? "style=\"opacity:0.6;\"" : "")}>
                    <div class="build-card-image">
                      {CardImage(b)}
                    </div>
                    <div class="build-card-body">
                      <div class="build-card-tags">
                        <span class="tag {StatusClass(status)}">{Esc(status)}</span>
                        {string.Concat(categories.Select(c => $"<span class=\"tag {CategoryClass(c)}\">{Esc(c)}</span>"))}
                        {(b["originalPrice"] is null ? "" : "<span class=\"tag tag-sale\">Sale</span>")}
                      </div>
                      <div class="build-card-title">{Esc(Text(b, "name"))}</div>
                      <ul class="build-specs">{string.Concat(specs.Select(s => $"<li>{SpecIcon(s.Key, "bi-dot")} {Esc(s.Value)}</li>"))}</ul>
                      <div class="build-card-footer">
                        <div>{PriceHtml(b, "build-price")}</div>
                        <a href="builds/{Text(b, "id")}/" class="btn-view">{(isSold ? "View Sold Build" : "View Build")} <i class="bi bi-arrow-right"></i></a>
                      </div>
                    </div>
                  </div>
                </div>

        """;
}

// Copy of buildHomeSlide() in main.js
string HomeSlide(JsonObject b, int index)
{
    string status = Text(b, "status");
    var categories = Strings(b, "category").Select(c => $"<span class=\"tag {CategoryClass(c)}\">{Esc(c)}</span>");
    var specs = Pairs(b, "specs").Take(4).Select(s => $"<li>{SpecIcon(s.Key, "bi-dot")} {Esc(s.Value)}</li>");
    return $"""

              <div class="carousel-item {(index == 0 ? "active" : "")}">
                <div class="featured-carousel-slide">
                  <div class="row g-0">
                    <div class="col-lg-6">
                      <div class="carousel-img-wrap">
                        {CardImage(b)}
                      </div>
                    </div>
                    <div class="col-lg-6">
                      <div class="carousel-content">
                        <div class="build-card-tags mb-3">
                          <span class="tag {(status == "Sold" ? "tag-sold" : "tag-available")}">{Esc(status)}</span>
                          {string.Concat(categories)}
                        </div>
                        <h3 style="font-size:var(--text-xl);margin-bottom:12px;">{Esc(Text(b, "name"))}</h3>
                        <p style="color:var(--text-muted);font-size:var(--text-sm);margin-bottom:20px;line-height:1.6;">{Esc(Text(b, "tagline"))}</p>
                        <ul class="build-specs mb-4">{string.Concat(specs)}</ul>
                        <div style="display:flex;align-items:center;gap:20px;flex-wrap:wrap;">
                          <span class="build-price">{Price(b["price"])}</span>
                          <a href="builds/{Text(b, "id")}/" class="btn-view">View Build <i class="bi bi-arrow-right"></i></a>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
        """;
}

// Copy of fillHeroBuildCard() in main.js. With no build for sale it keeps the generic card.
string HeroCard(JsonObject? b)
{
    string status = b is null ? "Available" : Text(b, "status");
    string color = status switch { "In-Progress" => "#f59e0b", "Reserved" => "#E05252", _ => "#3DAA6E" };
    string specs = b is null ? "" : string.Concat(new[] { "CPU", "GPU", "RAM", "Storage" }
        .Select(key => (key, value: Text(b["specs"]?.AsObject(), key)))
        .Where(s => s.value != "")
        .Select(s => $"""

                    <div style="display:flex;align-items:center;gap:10px;font-size:var(--text-sm);color:var(--text-muted);">
                      <i class="bi {SpecIcon(s.key, "bi-dot", iconOnly: true)}" style="color:var(--accent);width:16px;"></i> {Esc(s.value)}
                    </div>
            """));
    return $"""
                      <div style="display:flex;align-items:center;gap:10px;margin-bottom:20px;">
                        <div id="heroBuildDot" style="width:10px;height:10px;border-radius:50%;background:{color};"></div>
                        <span id="heroBuildStatus" style="font-size:var(--text-xs);color:{color};font-family:'JetBrains Mono',monospace;">{(status == "In-Progress" ? "Coming soon" : Esc(status))}</span>
                      </div>
                      <div id="heroBuildName" style="font-family:'Space Grotesk',sans-serif;font-weight:700;font-size:1.1rem;margin-bottom:4px;">{(b is null ? "Custom Builds" : Esc(Text(b, "name")))}</div>
                      <div id="heroBuildTagline" style="font-size:var(--text-xs);color:var(--text-muted);margin-bottom:20px;">{(b is null ? "Built, benchmarked and ready to go" : Esc(Text(b, "tagline")))}</div>
                      <div id="heroBuildSpecs" style="display:flex;flex-direction:column;gap:10px;margin-bottom:24px;">{specs}</div>
                      <div style="display:flex;align-items:center;justify-content:space-between;padding-top:16px;border-top:1px solid var(--border);">
                        <span id="heroBuildPrice" style="font-family:'JetBrains Mono',monospace;font-size:1.4rem;color:var(--text-primary);">{(b is null ? "" : Price(b["price"]))}</span>
                        <a id="heroBuildLink" href="{(b is null ? "builds.html" : $"builds/{Text(b, "id")}/")}" style="color:var(--accent);font-size:var(--text-sm);font-weight:600;display:flex;align-items:center;gap:6px;">
                          View <i class="bi bi-arrow-right"></i>
                        </a>
                      </div>

        """;
}

string CardImage(JsonObject b)
{
    string image = Text(b, "featuredImage");
    return image == ""
        ? "<div class=\"build-card-image-placeholder\" style=\"height:220px;\"><i class=\"bi bi-pc-display-horizontal\" style=\"font-size:3rem;color:var(--border);\"></i></div>"
        : $"<img src=\"{Esc(image)}\" alt=\"{Esc(Text(b, "name"))}\" loading=\"lazy\">";
}

string PriceHtml(JsonObject b, string cssClass) =>
    (b["originalPrice"] is null ? "" : $"<span class=\"build-price-old\">{Price(b["originalPrice"])}</span> ")
    + $"<span class=\"{cssClass}\">{Price(b["price"])}</span>";


// =====================================================================
//  Small helpers
// =====================================================================

// Reads a text value. Missing keys give "" so one gap doesn't crash the run.
string Text(JsonObject? obj, string key, string fallback = "") =>
    obj?[key] is JsonValue v ? v.ToString() : fallback;

bool Flag(JsonObject b, string key) => b[key] is JsonValue v && v.TryGetValue(out bool on) && on;

bool IsForSale(JsonObject b) => Text(b, "status") is not ("Sold" or "Archived");

List<string> Strings(JsonObject b, string key) =>
    b[key] is JsonArray arr ? arr.Select(n => n?.ToString() ?? "").Where(s => s != "").ToList() : [];

List<KeyValuePair<string, string>> Pairs(JsonObject b, string key) =>
    b[key] is JsonObject obj ? obj.Select(kv => KeyValuePair.Create(kv.Key, kv.Value?.ToString() ?? "")).ToList() : [];

// 4000 → "$4,000" (same as formatPrice in the JS)
string Price(JsonNode? price) =>
    price is null ? "" : "$" + price.GetValue<decimal>().ToString("#,0.##", CultureInfo.GetCultureInfo("en-AU"));

string StatusClass(string status) => status switch
{
    "Available" => "tag-available",
    "In-Progress" => "tag-in-progress",
    "Reserved" => "tag-sale",
    _ => "tag-sold",
};

string CategoryClass(string category) => category switch
{
    "gaming" => "tag-gaming",
    "workstation" => "tag-workstation",
    "custom" => "tag-custom",
    "highend" => "tag-highend",
    "sale" => "tag-sale",
    _ => "",
};

// Bootstrap icon for a spec name. Returns the <i> tag, or just the class name with iconOnly.
string SpecIcon(string key, string fallback, bool iconOnly = false)
{
    string icon = key switch
    {
        "CPU" => "bi-cpu",
        "GPU" => "bi-gpu-card",
        "RAM" => "bi-memory",
        "Storage" => "bi-device-ssd",
        "Motherboard" => "bi-motherboard",
        "Cooling" => "bi-thermometer-half",
        "PSU" => "bi-plug",
        "Case" => "bi-box",
        "OS" => "bi-windows",
        "Extras" => "bi-stars",
        _ => fallback,
    };
    if (iconOnly) return icon;
    return icon == "" ? "" : $"<i class=\"bi {icon}\"></i>";
}

// Makes text safe to put inside HTML: < > & " ' can't be read as tags or attribute ends.
string Esc(string text) => text
    .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
    .Replace("\"", "&quot;").Replace("'", "&#39;");

// Puts content inside the element with this id, e.g. <h1 id="buildName"></h1>.
// Only used on elements that are empty or plain text in the template.
string Fill(string html, string id, string inner)
{
    var pattern = new Regex($"(<(?<tag>\\w+)\\b[^>]*\\bid=\"{id}\"[^>]*>)(?:(?!</\\k<tag>>).)*(</\\k<tag>>)", RegexOptions.Singleline);
    if (!pattern.IsMatch(html)) throw new Exception($"build.html is missing an element with id=\"{id}\"");
    return pattern.Replace(html, m => m.Groups[1].Value + inner + m.Groups[2].Value, 1);
}

// Swaps everything between <!-- GENERATED:NAME --> and <!-- /GENERATED:NAME -->
string ReplaceBetween(string html, string name, string content)
{
    string start = $"<!-- GENERATED:{name} -->", end = $"<!-- /GENERATED:{name} -->";
    int a = html.IndexOf(start), b = html.IndexOf(end);
    if (a < 0 || b < a) throw new Exception($"Can't find the {start} ... {end} markers");
    return html[..(a + start.Length)] + content + html[b..];
}

// Like string.Replace, but stops with a clear message if the template text changed.
string MustReplace(string html, string find, string replacement)
{
    if (!html.Contains(find)) throw new Exception($"build.html no longer contains: {find}");
    return html.Replace(find, replacement);
}

void UpdateFile(string relativePath, Func<string, string> edit)
{
    string path = Path.Combine(root, relativePath);
    WriteIfChanged(path, edit(File.ReadAllText(path)));
}

// Only touches files whose content actually changed, so git diffs stay small.
void WriteIfChanged(string path, string content)
{
    content = content.Replace("\r\n", "\n");
    if (File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == content) return;
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, content, new UTF8Encoding(false));
    changed.Add(Path.GetRelativePath(root, path).Replace('\\', '/'));
}
