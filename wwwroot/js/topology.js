/* AtlasNOC — Topology canvas (Cytoscape.js) */
(function () {
  'use strict';

  var cy = null;
  var nodesById = {};

  var TYPE_ICON = {
    Router: '⇄', Switch: '⇅', Firewall: '⛔', Server: '♟', AccessPoint: '✧',
    Printer: '▦', IoT: '◫', Other: '◈', Unknown: '●'
  };
  var TYPE_COLOR = {
    Router: '#d29922', Switch: '#39c5cf', Firewall: '#f85149', Server: '#bc8cff',
    AccessPoint: '#58a6ff', Printer: '#8b98ab', IoT: '#3fb950', Other: '#8b98ab', Unknown: '#8b98ab'
  };
  var STATUS_COLOR = { Up: '#3fb950', Down: '#f85149', Degraded: '#d29922', Unknown: '#8b98ab' };

  function buildNodes(map) {
    nodesById = {};
    var elements = [];
    (map.nodes || []).forEach(function (n) {
      var type = (n.deviceType && n.deviceType) || 'Unknown';
      var status = (n.status || 'Unknown');
      nodesById[n.deviceId] = n;
      elements.push({
        data: {
          id: n.deviceId, label: n.hostname || n.ipAddress,
          ip: n.ipAddress, type: type, status: status,
          vendor: n.vendor || '', x: n.x, y: n.y
        }
      });
    });
    (map.links || []).forEach(function (l) {
      elements.push({
        data: {
          id: l.id, source: l.sourceNodeId, target: l.targetNodeId,
          linkType: l.type, status: l.status || 'Unknown'
        }
      });
    });
    return elements;
  }

  function nodeStyle() {
    return {
      selector: 'node',
      style: {
        label: 'data(label)',
        'font-size': 11,
        'text-valign': 'bottom',
        'text-margin-y': 4,
        'text-wrap': 'wrap',
        'text-max-width': '90px',
        color: '#d6deeb',
        'background-color': function (ele) {
          return TYPE_COLOR[ele.data('type')] || TYPE_COLOR.Unknown;
        },
        'border-width': 3,
        'border-color': function (ele) {
          return STATUS_COLOR[ele.data('status')] || STATUS_COLOR.Unknown;
        },
        width: 32, height: 32,
        shape: 'ellipse'
      }
    };
  }

  function init(map) {
    var container = document.getElementById('topologyCy');
    if (!container) return;
    var elements = buildNodes(map);

    cy = window.cytoscape({
      container: container,
      elements: elements,
      style: [
        nodeStyle(),
        {
          selector: 'edge',
          style: {
            width: 2,
            'line-color': '#30394a',
            'target-arrow-shape': 'triangle',
            'target-arrow-color': '#30394a',
            'curve-style': 'bezier',
            label: 'data(linkType)',
            'font-size': 8,
            'text-rotation': 'autorotate',
            color: '#8b98ab',
            'target-arrow-fill': 'hollow'
          }
        }
      ],
      layout: { name: 'preset', fit: true, padding: 30 },
      wheelSensitivity: 0.25,
      minZoom: 0.1,
      maxZoom: 4
    });

    // Down edges distinguished
    cy.edges().forEach(function (edge) {
      if (edge.data('status') === 'Down') {
        edge.style('line-color', '#f85149');
        edge.style('target-arrow-color', '#f85149');
      }
    });

    cy.on('tap', 'node', function (evt) { showDetail(evt.target.id()); });
    cy.on('tap', function (evt) {
      if (evt.target === cy) hideDetail();
    });

    wireToolbar();
  }

  function positionPreset(map) {
    // Simple grid fallback layout is handled by 'preset' using provided x/y; if none provided, lay out.
  }

  function wireToolbar() {
    var el;
    function bind(id, fn) {
      el = document.getElementById(id);
      if (el) el.addEventListener('click', fn);
    }
    bind('topoFit', function () { if (cy) cy.fit(undefined, 30); });
    bind('topoZoomIn', function () { if (cy) cy.zoom(cy.zoom() * 1.25); });
    bind('topoZoomOut', function () { if (cy) cy.zoom(cy.zoom() * 0.8); });
    bind('topoCenter', function () { if (cy) cy.center(); });

    var search = document.getElementById('topoSearch');
    el = document.getElementById('topoSearchBtn');
    if (el && search) el.addEventListener('click', function () { doSearch(search.value); });
    if (search) search.addEventListener('keydown', function (e) { if (e.key === 'Enter') doSearch(search.value); });

    var filter = document.getElementById('topoStatusFilter');
    if (filter) filter.addEventListener('change', function () { applyFilter(filter.value); });

    el = document.getElementById('topoRefresh');
    if (el) el.addEventListener('click', function () {
      window.AtlasNoc.request('/topology/json', {}, { showError: false })
        .then(function (r) { return r.json(); })
        .then(function (map) { if (cy) { cy.destroy(); } init(map); })
        .catch(function () { window.AtlasNoc.toast('No se pudo refrescar la topología.', 'danger'); });
    });
  }

  function doSearch(value) {
    if (!cy) return;
    var q = (value || '').trim().toLowerCase();
    if (!q) return;
    cy.nodes().forEach(function (n) {
      var label = ((n.data('label') || '') + ' ' + (n.data('ip') || '')).toLowerCase();
      n.style('border-color', label.indexOf(q) >= 0 ? '#58a6ff' : (STATUS_COLOR[n.data('status')] || STATUS_COLOR.Unknown));
    });
    var hit = cy.nodes().filter(function (n) {
      return ((n.data('label') || '') + ' ' + (n.data('ip') || '')).toLowerCase().indexOf(q) >= 0;
    });
    if (hit.length) {
      cy.animate({ center: { eles: hit }, zoom: Math.max(cy.zoom(), 1.6) }, { duration: 400 });
      hit.forEach(function (n) { n.addClass('search-hit'); });
    }
  }

  function applyFilter(status) {
    if (!cy) return;
    cy.nodes().forEach(function (n) {
      var show = !status || n.data('status') === status;
      n.style('display', show ? 'element' : 'none');
    });
    cy.edges().forEach(function (e) {
      var sShow = e.source().style('display') !== 'none';
      var tShow = e.target().style('display') !== 'none';
      e.style('display', (sShow && tShow) ? 'element' : 'none');
    });
    cy.fit(undefined, 30);
  }

  function showDetail(id) {
    var n = nodesById[id];
    var panel = document.getElementById('topoDetail');
    var body = document.getElementById('topoDetailBody');
    if (!n || !panel || !body) return;
    var type = n.deviceType || 'Unknown';
    var status = n.status || 'Unknown';
    body.innerHTML =
      '<div class="noc-card" style="margin:0;"><h2 class="noc-card-h">' + window.AtlasNoc.escape(n.hostname || n.ipAddress) + '</h2>' +
      '<dl class="noc-meta">' +
      '<div><dt>IP</dt><dd class="mono">' + window.AtlasNoc.escape(n.ipAddress) + '</dd></div>' +
      '<div><dt>Tipo</dt><dd>' + window.AtlasNoc.escape(type) + '</dd></div>' +
      '<div><dt>Estado</dt><dd><span class="noc-badge ' + window.AtlasNoc.escape(status) + '">' + window.AtlasNoc.escape(status) + '</span></dd></div>' +
      (n.vendor ? '<div><dt>Vendor</dt><dd>' + window.AtlasNoc.escape(n.vendor) + '</dd></div>' : '') +
      '<div><dt>Interfaces</dt><dd>' + (n.interfaces ? n.interfaces.length : 0) + '</dd></div>' +
      '</dl>' +
      '<div style="display:flex; gap:8px; flex-wrap:wrap;">' +
      '<a class="noc-btn noc-btn-sm" href="/devices/' + window.AtlasNoc.escape(n.deviceId) + '">Ver dispositivo</a>' +
      '<a class="noc-btn noc-btn-sm" href="/metrics?deviceId=' + window.AtlasNoc.escape(n.deviceId) + '">Métricas</a>' +
      '</div></div>';
    panel.hidden = false;
  }

  function hideDetail() {
    var panel = document.getElementById('topoDetail');
    if (panel) panel.hidden = true;
  }

  window.AtlasNocTopology = { init: init };
})();