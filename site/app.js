// Лендінг: винесено з інлайн-<script>, щоб працював CSP script-src 'self'.
(function () {
  'use strict';
  var io = new IntersectionObserver(function (es) {
    es.forEach(function (e) {
      if (e.isIntersecting) { e.target.classList.add('vis'); io.unobserve(e.target); }
    });
  }, { threshold: 0.12 });
  document.querySelectorAll('.reveal').forEach(function (el) { io.observe(el); });

  var lightbox = document.getElementById('lightbox');
  document.querySelectorAll('.shot').forEach(function (s) {
    s.addEventListener('click', function () {
      var i = s.querySelector('img');
      document.getElementById('lbImg').src = i.src;
      document.getElementById('lbCap').textContent = s.querySelector('h3').textContent;
      lightbox.classList.add('open');
    });
  });
  lightbox.addEventListener('click', function () { lightbox.classList.remove('open'); });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') lightbox.classList.remove('open');
  });

  var toTop = document.getElementById('toTop');
  toTop.addEventListener('click', function () { scrollTo({ top: 0, behavior: 'smooth' }); });
  addEventListener('scroll', function () {
    toTop.classList.toggle('show', scrollY > 700);
  });

  var burger = document.getElementById('burger');
  if (burger) {
    burger.addEventListener('click', function () {
      document.querySelector('.nav-links').classList.toggle('open');
    });
  }

  document.getElementById('year').textContent = new Date().getFullYear();

  // counters
  var cio = new IntersectionObserver(function (es) {
    es.forEach(function (e) {
      if (!e.isIntersecting) return;
      var b = e.target;
      var end = parseFloat(b.dataset.count);
      var suf = b.dataset.suffix || '';
      var t0 = performance.now();
      var step = function (t) {
        var p = Math.min(1, (t - t0) / 1200);
        b.textContent = (end % 1 ? (end * p).toFixed(1) : Math.round(end * p)) + suf;
        if (p < 1) requestAnimationFrame(step);
      };
      requestAnimationFrame(step);
      cio.unobserve(b);
    });
  }, { threshold: 0.5 });
  document.querySelectorAll('[data-count]').forEach(function (el) { cio.observe(el); });

  // img fallback
  document.querySelectorAll('.shot img').forEach(function (img) {
    img.loading = 'lazy';
    img.onerror = function () {
      img.style.display = 'none';
      img.parentElement.innerHTML = '<div style="padding:60px 20px;text-align:center;color:#5b6577;font-size:3rem">🖼️</div>';
    };
  });
})();
