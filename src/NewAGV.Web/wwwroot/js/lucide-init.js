(function () {
  function run() {
    if (!window.lucide || typeof window.lucide.createIcons !== 'function') {
      return;
    }

    window.lucide.createIcons({
      attrs: {
        class: ['lucide-icon'],
        'stroke-width': 1.9
      }
    });
  }

  let rafId = 0;
  function schedule() {
    if (rafId) {
      return;
    }

    rafId = window.requestAnimationFrame(() => {
      rafId = 0;
      run();
    });
  }

  window.newAgvLucide = { refresh: schedule };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', schedule, { once: true });
  } else {
    schedule();
  }

  const observer = new MutationObserver(() => schedule());
  observer.observe(document.body, {
    childList: true,
    subtree: true
  });
})();