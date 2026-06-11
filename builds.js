/* =========================================
   BUILDS.JS
   Handles both:
   - builds.html  → generates card grid from JSON
   - build.html   → generates full build detail page from JSON
   ========================================= */

// ── Spec icons mapping — Bootstrap Icons for common spec types
const SPEC_ICONS = {
  'CPU':        'bi-cpu',
  'GPU':        'bi-gpu-card',
  'RAM':        'bi-memory',
  'Storage':    'bi-device-ssd',
  'Motherboard':'bi-motherboard',
  'Cooling':    'bi-thermometer-half',
  'PSU':        'bi-plug',
  'Case':       'bi-box',
  'OS':         'bi-windows',
  'Extras':     'bi-stars',
};

// ── Status → tag class mapping
const STATUS_CLASSES = {
  'Available':  'tag-available',
  'Reserved':   'tag-sale',
  'Sold':       'tag-sold',
  'Archived':   'tag-sold',
};

// ── Category → tag class mapping
const CATEGORY_CLASSES = {
  'gaming':      'tag-gaming',
  'workstation': 'tag-workstation',
  'custom':      'tag-custom',
  'highend':     'tag-highend',
  'sale':        'tag-sale',
};

/* =========================================
   SHARED: Fetch builds.json
   ========================================= */
async function fetchBuilds() {
  const response = await fetch('data/builds.json');
  if (!response.ok) throw new Error('Failed to load builds.json');
  return response.json();
}

/* =========================================
   SHARED: Format price as AUD
   ========================================= */
function formatPrice(price) {
  return '$' + price.toLocaleString('en-AU');
}

/* =========================================
   SHARED: Build a mini card (used on builds.html + build.html "other builds")
   ========================================= */
function buildMiniCard(build) {
  // Work out which categories to show as tags
  const catTags = (build.category || [])
    .map(c => `<span class="tag ${CATEGORY_CLASSES[c] || ''}">${c}</span>`)
    .join('');

  // Price display (with original/sale price if present)
  const priceHTML = build.originalPrice
    ? `<span class="build-price-old">${formatPrice(build.originalPrice)}</span>
       <span class="build-price">${formatPrice(build.price)}</span>`
    : `<span class="build-price">${formatPrice(build.price)}</span>`;

  // Image or placeholder
  const imageHTML = build.featuredImage
    ? `<img src="${build.featuredImage}" alt="${build.name}" loading="lazy"
            onerror="this.parentElement.innerHTML='<div class=\'build-card-image-placeholder\' style=\'height:220px\'><i class=\'bi bi-pc-display-horizontal\' style=\'font-size:3rem;color:var(--border)\'></i></div>'">`
    : `<div class="build-card-image-placeholder" style="height:220px;">
         <i class="bi bi-pc-display-horizontal" style="font-size:3rem;color:var(--border);"></i>
       </div>`;

  // Show top 4 specs as list items
  const specEntries = Object.entries(build.specs || {}).slice(0, 4);
  const specIcon = (key) => SPEC_ICONS[key] ? `<i class="bi ${SPEC_ICONS[key]}"></i>` : '<i class="bi bi-dot"></i>';
  const specsHTML = specEntries
    .map(([k, v]) => `<li>${specIcon(k)} ${v}</li>`)
    .join('');

  // CTA — sold builds link back to builds page, others to their build page
  const ctaHref = build.status === 'Sold' ? 'builds.html' : `build.html?id=${build.id}`;
  const ctaText = build.status === 'Sold' ? 'See Available Builds' : 'View Build';

  // Opacity for sold
  const cardStyle = build.status === 'Sold' ? 'style="opacity:0.6;"' : '';

  // Categories as a space-separated string for filtering
  const categoryStr = [
    ...(build.category || []),
    build.status === 'Available' ? 'available' : '',
    build.status === 'Sold' ? 'sold' : '',
  ].filter(Boolean).join(' ');

  return `
    <div class="col-lg-4 col-md-6 build-col animate-fade-up">
      <div class="build-card" data-category="${categoryStr}" ${cardStyle}>
        <div class="build-card-image">
          ${imageHTML}
        </div>
        <div class="build-card-body">
          <div class="build-card-tags">
            <span class="tag ${STATUS_CLASSES[build.status] || 'tag-sold'}">${build.status}</span>
            ${catTags}
            ${build.originalPrice ? '<span class="tag tag-sale">Sale</span>' : ''}
          </div>
          <div class="build-card-title">${build.name}</div>
          <ul class="build-specs">${specsHTML}</ul>
          <div class="build-card-footer">
            <div>${priceHTML}</div>
            <a href="${ctaHref}" class="btn-view">${ctaText} <i class="bi bi-arrow-right"></i></a>
          </div>
        </div>
      </div>
    </div>
  `;
}

/* =========================================
   BUILDS.HTML — Generate grid + wire filters
   ========================================= */
async function initBuildsPage() {
  const grid = document.getElementById('buildsGrid');
  const loadingState = document.getElementById('loadingState');
  const errorState = document.getElementById('errorState');

  if (!grid) return; // Not on builds page

  try {
    const builds = await fetchBuilds();

    // Sort: Available first, then others, then Sold last
    builds.sort((a, b) => {
      const order = { 'Available': 0, 'Reserved': 1, 'Archived': 2, 'Sold': 3 };
      return (order[a.status] ?? 99) - (order[b.status] ?? 99);
    });

    // Remove loading spinner
    loadingState.remove();

    // Inject cards
    grid.innerHTML = builds.map(buildMiniCard).join('');

    // Re-run scroll animation observer for new elements
    document.querySelectorAll('.animate-fade-up').forEach(el => {
      buildObserver.observe(el);
    });

    // Wire up filter buttons
    const filterBtns = document.querySelectorAll('.filter-btn');
    filterBtns.forEach(btn => {
      btn.addEventListener('click', () => {
        filterBtns.forEach(b => b.classList.remove('active'));
        btn.classList.add('active');

        const filter = btn.dataset.filter;
        document.querySelectorAll('.build-col').forEach(col => {
          const card = col.querySelector('[data-category]');
          if (!card) return;
          const cats = card.dataset.category;
          col.style.display = (filter === 'all' || cats.includes(filter)) ? '' : 'none';
        });
      });
    });

  } catch (err) {
    console.error(err);
    loadingState.classList.add('d-none');
    errorState.classList.remove('d-none');
  }
}

/* =========================================
   BUILD.HTML — Generate full detail page
   ========================================= */
async function initBuildPage() {
  const buildContent = document.getElementById('buildContent');
  const loadingState = document.getElementById('loadingState');
  const errorState = document.getElementById('errorState');

  if (!buildContent) return; // Not on build page

  // Read ?id= from URL
  const params = new URLSearchParams(window.location.search);
  const buildId = params.get('id');

  function showError() {
    loadingState.style.display = 'none';
    errorState.classList.remove('d-none');
  }

  if (!buildId) { showError(); return; }

  try {
    const builds = await fetchBuilds();
    const build = builds.find(b => b.id === buildId);

    if (!build) { showError(); return; }

    // ── Update page meta
    document.getElementById('pageTitle').textContent = `${build.name} — James | Builds`;
    document.getElementById('metaDesc').setAttribute('content', build.tagline);
    document.getElementById('breadcrumbName').textContent = build.name;

    // ── Hero: main image
    const mainImg = document.getElementById('mainImage');
    if (build.featuredImage) {
      mainImg.src = build.featuredImage;
      mainImg.alt = build.name;
      mainImg.onerror = () => { mainImg.style.display = 'none'; };
    } else {
      mainImg.style.display = 'none';
    }

    // ── Status badge on image
    document.getElementById('statusBadgeHero').innerHTML =
      `<span class="tag ${STATUS_CLASSES[build.status] || 'tag-sold'}">${build.status}</span>`;

    // ── Thumbnail strip
    const thumbStrip = document.getElementById('thumbStrip');
    if (build.gallery && build.gallery.length > 1) {
      thumbStrip.innerHTML = build.gallery.map((img, i) => `
        <div class="build-thumb ${i === 0 ? 'active' : ''}" onclick="switchImage('${img}', this)">
          <img src="${img}" alt="Gallery image ${i+1}" loading="lazy"
               onerror="this.parentElement.style.display='none'">
        </div>
      `).join('');
    }

    // ── Hero tags
    const catTags = (build.category || [])
      .map(c => `<span class="tag ${CATEGORY_CLASSES[c] || ''}">${c}</span>`)
      .join('');
    document.getElementById('heroTags').innerHTML = catTags;

    // ── Build name + tagline
    document.getElementById('buildName').textContent = build.name;
    document.getElementById('buildTagline').textContent = build.tagline;

    // ── Price block
    const priceHTML = build.originalPrice
      ? `<span class="build-price-old">${formatPrice(build.originalPrice)}</span>
         <span class="build-page-price">${formatPrice(build.price)}</span>`
      : `<span class="build-page-price">${formatPrice(build.price)}</span>`;
    document.getElementById('priceBlock').innerHTML = priceHTML;

    // ── Quick specs (top 5 for the sidebar)
    const quickSpecEntries = Object.entries(build.specs || {}).slice(0, 5);
    document.getElementById('quickSpecs').innerHTML = quickSpecEntries.map(([k, v]) => {
      const icon = SPEC_ICONS[k] ? `<i class="bi ${SPEC_ICONS[k]}"></i>` : '';
      return `<div class="quick-spec-row"><span class="quick-spec-key">${icon} ${k}</span><span class="quick-spec-val">${v}</span></div>`;
    }).join('');

    // ── CTA buttons
    const isSold = build.status === 'Sold';
    if (isSold) {
      document.getElementById('ctaButtons').innerHTML = `
        <a href="builds.html" class="btn-primary-custom w-100 justify-content-center">
          Browse Available Builds <i class="bi bi-arrow-right"></i>
        </a>
      `;
    } else {
      document.getElementById('ctaButtons').innerHTML = `
        <a href="#enquiryForm" class="btn-primary-custom" onclick="scrollToForm(event)">
          ${build.cta?.primary || 'Enquire About This Build'} <i class="bi bi-arrow-right"></i>
        </a>
        <a href="#enquiryForm" class="btn-ghost-custom" onclick="scrollToForm(event, '${build.cta?.secondary || 'Reserve This Build'}')">
          ${build.cta?.secondary || 'Reserve This Build'}
        </a>
      `;
    }

    // ── Full specs table
    document.getElementById('specsTable').innerHTML = Object.entries(build.specs || {}).map(([k, v]) => {
      const icon = SPEC_ICONS[k] ? `<i class="bi ${SPEC_ICONS[k]}"></i>` : '';
      return `
        <div class="spec-row">
          <span class="spec-key">${icon} ${k}</span>
          <span class="spec-val">${v}</span>
        </div>
      `;
    }).join('');

    // ── Description
    document.getElementById('buildDescription').textContent = build.description;

    // ── Highlights
    document.getElementById('highlightsList').innerHTML = (build.highlights || [])
      .map(h => `<div class="highlight-item"><i class="bi bi-check-circle-fill"></i> ${h}</div>`)
      .join('');

    // ── Benchmarks
    const benchEntries = Object.entries(build.benchmarks || {});
    const benchmarkSection = document.getElementById('benchmarkSection');
    if (benchEntries.length === 0) {
      benchmarkSection.style.display = 'none';
    } else {
      document.getElementById('benchmarkCards').innerHTML = benchEntries.map(([k, v]) => `
        <div class="col-sm-6 col-lg-3">
          <div class="benchmark-card">
            <div class="benchmark-label">${k}</div>
            <div class="benchmark-value">${v}</div>
          </div>
        </div>
      `).join('');
    }

    // ── Enquiry form — auto-fill hidden fields
    document.getElementById('formBuildName').value = build.name;
    document.getElementById('formBuildId').value = build.id;
    document.getElementById('formBuildPrice').value = formatPrice(build.price);

    // ── Enquiry banner above form
    document.getElementById('enquiryBanner').innerHTML = isSold ? '' : `
      <div class="enquiry-build-banner">
        <div>
          <div style="font-size:var(--text-xs);color:var(--text-muted);text-transform:uppercase;letter-spacing:0.08em;margin-bottom:4px;">Enquiring about</div>
          <div style="font-weight:700;font-family:'Space Grotesk',sans-serif;">${build.name}</div>
        </div>
        <div style="font-family:'JetBrains Mono',monospace;font-size:var(--text-lg);color:var(--text-primary);">${formatPrice(build.price)}</div>
      </div>
    `;

    // ── Other builds (exclude current, exclude archived, max 3)
    const others = builds
      .filter(b => b.id !== build.id && b.status !== 'Archived')
      .slice(0, 3);
    document.getElementById('otherBuilds').innerHTML = others.map(buildMiniCard).join('');

    // ── Show the content, hide loading
    loadingState.style.display = 'none';
    buildContent.classList.remove('d-none');

    // ── Run scroll animations on newly rendered elements
    document.querySelectorAll('.animate-fade-up').forEach(el => buildObserver.observe(el));

  } catch (err) {
    console.error(err);
    showError();
  }
}

/* =========================================
   Gallery: switch main image on thumb click
   ========================================= */
function switchImage(src, thumbEl) {
  document.getElementById('mainImage').src = src;
  document.querySelectorAll('.build-thumb').forEach(t => t.classList.remove('active'));
  thumbEl.classList.add('active');
}

/* =========================================
   Smooth scroll to enquiry form + pre-select type
   ========================================= */
function scrollToForm(e, label) {
  e.preventDefault();
  const form = document.querySelector('.contact-form-wrap');
  if (form) form.scrollIntoView({ behavior: 'smooth', block: 'start' });
  if (label) {
    // Map button label to select value
    const map = {
      'Reserve This Build': 'reserve',
      'Request More Information': 'info',
      'Make an Offer': 'offer',
    };
    const sel = document.getElementById('enquiryType');
    if (sel && map[label]) sel.value = map[label];
  }
}

/* =========================================
   Shared IntersectionObserver for fade-up animations
   ========================================= */
const buildObserver = new IntersectionObserver((entries) => {
  entries.forEach((entry, i) => {
    if (entry.isIntersecting) {
      setTimeout(() => entry.target.classList.add('visible'), i * 60);
      buildObserver.unobserve(entry.target);
    }
  });
}, { threshold: 0.1, rootMargin: '0px 0px -40px 0px' });

/* =========================================
   Initialise — detect which page we're on
   ========================================= */
document.addEventListener('DOMContentLoaded', () => {
  if (document.getElementById('buildsGrid')) {
    initBuildsPage();
  }
  if (document.getElementById('buildContent')) {
    initBuildPage();
  }
});
