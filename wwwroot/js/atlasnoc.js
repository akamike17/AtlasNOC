/* AtlasNOC — UI global helpers */
(function () {
  'use strict';

  // ── Sidebar collapse toggle ─────────────────────────────────────────
  function initSidebar() {
    var toggle = document.getElementById('sidebarToggle');
    var app = document.querySelector('.noc-app');
    var key = 'atlasnoc.sidebarCollapsed';
    if (toggle && app) {
      function apply(v) {
        app.classList.toggle('sidebar-collapsed', v === '1');
      }
      // Desktop: restore preference; data-bs… not needed for pure CSS collapse.
      if (window.matchMedia('(min-width: 961px)').matches) {
        apply(localStorage.getItem(key));
      }
      toggle.addEventListener('click', function () {
        var collapsed = app.classList.toggle('sidebar-collapsed');
        if (window.matchMedia('(min-width: 961px)').matches) {
          try { localStorage.setItem(key, collapsed ? '1' : '0'); } catch (e) { /* ignore */ }
        }
      });
    }
  }

  // ── Toasts ───────────────────────────────────────────────────────────
  function toast(message, type /* success | danger | warning | info */) {
    type = type || 'info';
    var region = document.getElementById('toastRegion');
    if (!region) return;
    var el = document.createElement('div');
    el.className = 'toast align-items-center text-bg-' + (type === 'danger' ? 'danger'
      : type === 'success' ? 'success' : type === 'warning' ? 'warning' : 'dark');
    el.setAttribute('data-bs-delay', '5000');
    el.innerHTML = '<div class="d-flex"><div class="toast-body">' +
      window.AtlasNoc.escape(message) + '</div>' +
      '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button></div>';
    region.appendChild(el);
    var t = new bootstrap.Toast(el, { delay: 5000 });
    t.show();
    el.addEventListener('hidden.bs.toast', function () { el.remove(); });
  }

  var statusMessages = {
    400: 'Solicitud inválida. Revise los datos.',
    401: 'No autenticado o sesión expirada.',
    403: 'No tiene permisos para esta acción.',
    404: 'Recurso no encontrado.',
    409: 'Conflicto con el estado actual.',
    429: 'Demasiadas solicitudes. Espere un momento.',
    500: 'Error interno del servidor.'
  };

  // ── Safe fetch wrapper (handles errors; never leaks secrets) ─────────
  // Returns a Promise<Response>. On HTTP error it shows a toast with a
  // generic message (status codes only, no raw payloads).
  function request(url, options, opts) {
    options = options || {};
    opts = opts || {};
    options.headers = options.headers || {};
    options.headers['RequestVerificationToken'] =
      document.querySelector('input[name="__RequestVerificationToken"]')
        ? document.querySelector('input[name="__RequestVerificationToken"]').value
        : '';
    if (options.body && typeof options.body === 'object' && !(options.body instanceof FormData)) {
      options.headers['Content-Type'] = 'application/json; charset=utf-8';
      options.body = JSON.stringify(options.body);
    }
    options.credentials = 'same-origin';

    var controller = new AbortController();
    var timer = setTimeout(function () { controller.abort(); }, opts.timeoutMs || 30000);
    if (options.signal) options.signal.addEventListener('abort', function () { controller.abort(); });

    return fetch(url, Object.assign({}, options, { signal: controller.signal }))
      .then(function (res) {
        clearTimeout(timer);
        if (res.ok) return res;
        return res.json().catch(function () { return null; }).then(function (payload) {
          var msg = (opts.showError === false) ? null : (statusMessages[res.status] || ('Error (' + res.status + ').'));
          if (msg) window.AtlasNoc.toast(msg, 'danger');
          var err = new Error('HTTP ' + res.status);
          err.status = res.status;
          err.payload = payload;
          throw err;
        });
      })
      .catch(function (err) {
        clearTimeout(timer);
        if (err && err.name === 'AbortError') {
          if (opts.showError !== false) window.AtlasNoc.toast('La petición tardó demasiado.', 'warning');
          var e = new Error('Timeout');
          e.status = 408;
          throw e;
        }
        if (err && err.status) throw err;          // already handled above
        if (opts.showError !== false) window.AtlasNoc.toast('Fallo de conexión con el servidor.', 'danger');
        throw err;
      });
  }

  function escapeHtml(value) {
    return String(value == null ? '' : value)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  function formatAge(iso) {
    if (!iso) return '—';
    var d = new Date(iso);
    if (isNaN(d)) return '—';
    var mins = Math.floor((Date.now() - d.getTime()) / 60000);
    if (mins < 1) return 'ahora';
    if (mins < 60) return mins + 'm';
    var hours = Math.floor(mins / 60);
    if (hours < 24) return hours + 'h';
    return Math.floor(hours / 24) + 'd';
  }

  function formatDateTime(iso) {
    if (!iso) return '—';
    var d = new Date(iso);
    return isNaN(d) ? '—' : d.toLocaleString();
  }

  window.AtlasNoc = {
    init: function () { initSidebar(); },
    toast: toast,
    request: request,
    escape: escapeHtml,
    formatAge: formatAge,
    formatDateTime: formatDateTime
  };

  document.addEventListener('DOMContentLoaded', initSidebar);
})();