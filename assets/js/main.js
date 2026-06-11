/* =========================================
   PC BUILD CO. — MAIN.JS
   ========================================= */

document.addEventListener('DOMContentLoaded', () => {

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

  // ── Build filter (builds.html)
  const filterBtns = document.querySelectorAll('.filter-btn');
  const buildCards = document.querySelectorAll('[data-category]');

  filterBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      filterBtns.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');

      const filter = btn.dataset.filter;
      buildCards.forEach(card => {
        const parent = card.closest('.build-col');
        if (!parent) return;
        if (filter === 'all' || card.dataset.category.includes(filter)) {
          parent.style.display = '';
          card.closest('.build-col').classList.remove('d-none');
        } else {
          parent.style.display = 'none';
        }
      });
    });
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

  // ── Contact form (Formspree handler with AJAX)
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

});
