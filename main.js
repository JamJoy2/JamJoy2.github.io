/* =========================================
   JAMES BUILDS — MAIN.JS
   ========================================= */

document.addEventListener('DOMContentLoaded', () => {

  const buildIcons = {
    CPU: 'bi-cpu',
    GPU: 'bi-gpu-card',
    RAM: 'bi-memory',
    Storage: 'bi-device-ssd',
    Motherboard: 'bi-motherboard',
    Cooling: 'bi-thermometer-half',
    PSU: 'bi-plug',
    Case: 'bi-box',
    OS: 'bi-windows',
    Extras: 'bi-stars'
  };

  const categoryClassMap = {
    gaming: 'tag-gaming',
    workstation: 'tag-workstation',
    custom: 'tag-custom',
    highend: 'tag-highend',
    sale: 'tag-sale'
  };

  const formatPrice = (price) => '$' + price.toLocaleString('en-AU');

  const buildHomeSlide = (build, index) => {
    const categories = (build.category || [])
      .map(category => `<span class="tag ${categoryClassMap[category] || ''}">${category}</span>`)
      .join('');

    const imageHTML = build.featuredImage
      ? `<img src="${build.featuredImage}" alt="${build.name}" loading="lazy"
            onerror="this.parentElement.innerHTML='<div class=\\'build-card-image-placeholder\\' style=\\'height:220px\\'><i class=\\'bi bi-pc-display-horizontal\\' style=\\'font-size:3rem;color:var(--border)\\'></i></div>'">`
      : `<div class="build-card-image-placeholder" style="height:220px;">
           <i class="bi bi-pc-display-horizontal" style="font-size:3rem;color:var(--border);"></i>
         </div>`;

    const specEntries = Object.entries(build.specs || {}).slice(0, 4);
    const specsHTML = specEntries.map(([key, value]) => {
      const icon = buildIcons[key] ? `<i class="bi ${buildIcons[key]}"></i>` : '<i class="bi bi-dot"></i>';
      return `<li>${icon} ${value}</li>`;
    }).join('');

    return `
      <div class="carousel-item ${index === 0 ? 'active' : ''}">
        <div class="featured-carousel-slide">
          <div class="row g-0">
            <div class="col-lg-6">
              <div class="carousel-img-wrap">
                ${imageHTML}
              </div>
            </div>
            <div class="col-lg-6">
              <div class="carousel-content">
                <div class="build-card-tags mb-3">
                  <span class="tag ${build.status === 'Sold' ? 'tag-sold' : 'tag-available'}">${build.status}</span>
                  ${categories}
                </div>
                <h3 style="font-size:var(--text-xl);margin-bottom:12px;">${build.name}</h3>
                <p style="color:var(--text-muted);font-size:var(--text-sm);margin-bottom:20px;line-height:1.6;">${build.tagline}</p>
                <ul class="build-specs mb-4">${specsHTML}</ul>
                <div style="display:flex;align-items:center;gap:20px;flex-wrap:wrap;">
                  <span class="build-price">${formatPrice(build.price)}</span>
                  <a href="build.html?id=${build.id}" class="btn-view">View Build <i class="bi bi-arrow-right"></i></a>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    `;
  };

  const initHomeFeaturedBuilds = async () => {
    const homeCarouselInner = document.getElementById('homeFeaturedCarouselInner');
    if (!homeCarouselInner) return;

    try {
      const response = await fetch('data/builds.json');
      if (!response.ok) throw new Error('Failed to load builds.json');

      const builds = await response.json();
      const homeBuilds = builds.filter(build => build.homeFeatured).slice(0, 3);
      const fallbackBuilds = builds.filter(build => build.featured).slice(0, 3);
      const selectedBuilds = homeBuilds.length ? homeBuilds : fallbackBuilds;

      homeCarouselInner.innerHTML = selectedBuilds.map((build, index) => buildHomeSlide(build, index)).join('');
      document.querySelectorAll('#homeFeaturedCarousel .carousel-item').forEach(el => observer.observe(el));
    } catch (error) {
      console.error(error);
      homeCarouselInner.innerHTML = '';
    }
  };

  // ── Navbar scroll behaviour
  const navbar = document.getElementById('mainNav');
  if (navbar) {
    const onScroll = () => {
      if (window.scrollY > 40) {
        navbar.classList.add('scrolled');
      } else {
        navbar.classList.remove('scrolled');
      }
    };
    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();
  }

  // ── Active nav link
  const currentPage = location.pathname.split('/').pop() || 'index.html';
  document.querySelectorAll('.navbar-nav .nav-link').forEach(link => {
    const href = link.getAttribute('href');
    if (href === currentPage || (currentPage === '' && href === 'index.html')) {
      link.classList.add('active');
      link.style.color = 'var(--text-primary)';
    }
  });

  // ── Scroll animations
  const observerOpts = { threshold: 0.12, rootMargin: '0px 0px -40px 0px' };
  const observer = new IntersectionObserver((entries) => {
    entries.forEach((entry, i) => {
      if (entry.isIntersecting) {
        setTimeout(() => {
          entry.target.classList.add('visible');
        }, i * 60);
        observer.unobserve(entry.target);
      }
    });
  }, observerOpts);

  document.querySelectorAll('.animate-fade-up').forEach(el => observer.observe(el));

  initHomeFeaturedBuilds();

  // ── Contact form (web3forms handler with AJAX)
  const contactForm = document.getElementById('contactForm');
  if (contactForm) {
    const submitBtn = document.getElementById('submitBtn');
    const formSuccess = document.getElementById('formSuccess');

    contactForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      submitBtn.disabled = true;
      submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Sending...';

      try {
        const formData = new FormData(contactForm);
        const response = await fetch(contactForm.action, {
          method: 'POST',
          body: formData,
          headers: { 'Accept': 'application/json' }
        });

        if (response.ok) {
          contactForm.style.display = 'none';
          formSuccess.classList.add('show');
        } else {
          throw new Error('Form submission failed');
        }
      } catch (err) {
        submitBtn.disabled = false;
        submitBtn.innerHTML = 'Send Message <i class="bi bi-arrow-right"></i>';
        alert('Something went wrong. Please try again or reach out via Facebook Marketplace.');
      }
    });
  }

  // ── Stagger build cards on page load
  const buildCols = document.querySelectorAll('.build-col');
  buildCols.forEach((col, i) => {
    col.style.transitionDelay = `${i * 50}ms`;
  });

  // ── Reviews tab panel (null-checked — only exists on one page)
  const tab = document.getElementById('reviews-tab');
  const panel = document.getElementById('reviews-panel');
  const closeBtn = document.getElementById('close-reviews');

  if (tab && panel && closeBtn) {
    tab.onclick = () => panel.classList.add('open');
    closeBtn.onclick = () => panel.classList.remove('open');
  }

});
