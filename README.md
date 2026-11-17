# PC Build Co. — Website

--- 
we will take over the world ! (or just melb pc market) 

** this is my shrexy to do list and some random stuff ** 
---
** to do: **
- add photos of builds 
- dynamicly link current inv to json (make it scalable from 1-3 and i can select with tags)
- dynamicly add sold builds to bottom of blog page via tags
- add DNS (maybe free student one )
- add to service / pricing / add ABN and details 
- fix link at bottom of index page for insta / fb / yt icon 
---

## future admin dashboard 
management for drag from folder to the dashboard = read / wrights fills in json enterys for specs condition tags ETC 
toggle sold / avalable / pending 
kept on my personal pc so it cannot be used by anyone else lol
some sort of protection / backup version when adding in case somthing goes wrong 

---
## Adding or updating builds

1. Edit `data/builds.json`. Put photos in `images/builds/<id>/` and list them in `gallery`.
   - `id` becomes the web address (`/builds/<id>/`), so use lowercase letters, numbers and dashes only.
   - If you rename an id, add the old one to `"oldIds": ["old-id"]` so shared links keep working.
   - `status`: `Available`, `In-Progress`, `Reserved` or `Sold`. `homeFeatured: true` puts it on the home page.
2. Run the generator from this folder:
   ```
   dotnet run tools/generate.cs
   ```
   It writes the build pages, the cards on `builds.html` and `index.html`, `sitemap.xml` and `llms.txt`,
   so search engines and AI crawlers can read everything without JavaScript.
3. Commit and push. If you forget step 2, the "Check generated pages" action on GitHub goes red.

Don't hand-edit anything between `<!-- GENERATED:... -->` markers or inside `builds/`. It gets overwritten.

Use https://squoosh.app to compress photos before adding.

---

## Hosting on GitHub Pages

1. Create a new public GitHub repo
2. Push all files to the `main` branch
3. Go to Settings → Pages → Source: `main` / `root`
4. Site goes live at `https://yourusername.github.io/repo-name`

For a custom domain, add a `CNAME` file with your domain and configure DNS at your registrar.

---

## Stack

- HTML5, CSS3 (custom, no preprocessor)
- Bootstrap 5.3
- Bootstrap Icons 1.11
- Google Fonts (Space Grotesk, Inter, JetBrains Mono)
- Vanilla JS only
- Formspree for contact form
- GitHub Pages compatible (no server needed)
