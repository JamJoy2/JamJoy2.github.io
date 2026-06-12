# PC Build Co. — Website

--- 
we will take over the world ! (or just melb pc market) 

** this is my shrexy to do list and some random stuff ** 
---
** to do: **
- add photos of builds 
- dynamicly link current inv to json (make it scalable from 1-3 and i can select with tags)
- add DNS (maybe free student one )
- add to service / pricing / add ABN and details 
---

## future admin dashboard 
management for drag from folder to the dashboard = read / wrights fills in json enterys for specs condition tags ETC 
toggle sold / avalable / pending 
kept on my personal pc so it cannot be used by anyone else lol
some sort of protection / backup version when adding in case somthing goes wrong 

---
## Adding new builds

Open `builds.html` and duplicate one of the `<div class="col-lg-4 col-md-6 build-col">` blocks.

Update:
- `data-category` on the inner card (options: `gaming`, `workstation`, `custom`, `available`, `sale`, `sold`)
- Tags (`.tag-gaming`, `.tag-available`, etc.)
- Title, specs, and price
- Link href (Facebook Marketplace listing, or `contact.html`)

---

## Adding photos

Drop WebP images (max 200KB each) into `assets/img/builds/`.

Replace the placeholder div:
```html
<div class="build-card-image-placeholder" style="height:220px;">
  <i class="bi bi-pc-display-horizontal" ...></i>
</div>
```
With:
```html
<div class="build-card-image">
  <img src="assets/img/builds/your-photo.webp" alt="Build name" loading="lazy">
</div>
```

Use https://squoosh.app to compress images before adding.

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
