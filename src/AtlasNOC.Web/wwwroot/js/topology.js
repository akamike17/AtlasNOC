/* Presentación: el grafo llega del backend; el navegador no inventa relaciones. */
window.renderTopology = function (elementId, graph) {
  const target = document.getElementById(elementId);
  if (!target) return null;
  target.replaceChildren();
  if (!window.cytoscape) { target.textContent = 'No se pudo cargar el motor de topología.'; return null; }
  const get = (o, camel, pascal) => o?.[camel] ?? o?.[pascal];
  const rawNodes = get(graph, 'nodes', 'Nodes') || [], rawEdges = get(graph, 'edges', 'Edges') || [];
  const nodes = rawNodes.map(n => { const id = String(get(n,'id','Id') ?? ''); const label = String(get(n,'label','Label') ?? get(n,'ip','Ip') ?? id); const ip = String(get(n,'ip','Ip') ?? ''); return { data: { id, label: ip && label !== ip ? `${label}\n${ip}` : label, ip, status: get(n,'status','Status'), vendor: get(n,'vendor','Vendor') } }; }).filter(n => n.data.id && n.data.label);
  const ids = new Set(nodes.map(n => n.data.id));
  const edges = rawEdges.map(e => ({ data: { id: String(get(e,'id','Id') ?? ''), source: String(get(e,'source','Source') ?? ''), target: String(get(e,'target','Target') ?? ''), confirmed: get(e,'isConfirmed','IsConfirmed') } })).filter(e => e.data.id && ids.has(e.data.source) && ids.has(e.data.target));
  const count = document.getElementById(`${elementId}-count`); if (count) count.textContent = `${nodes.length} dispositivos / ${edges.length} enlaces`;
  const empty = document.getElementById(`${elementId}-empty`); if (empty) { empty.hidden = nodes.length !== 0; empty.textContent = 'No hay dispositivos inventariados para este filtro.'; }
  if (!nodes.length) return null;
  const cy = cytoscape({ container: target, elements: [...nodes, ...edges], style: [
    { selector: 'node', style: { label: 'data(label)', 'background-color': '#198754', color: '#17202a', 'text-valign': 'bottom', 'text-margin-y': 8, 'font-size': 11 } },
    { selector: 'edge', style: { 'line-color': '#6c757d', width: 2, 'curve-style': 'bezier' } },
    { selector: 'edge[confirmed = false]', style: { 'line-style': 'dashed', 'line-color': '#8b949e' } }
  ], layout: { name: edges.length ? 'breadthfirst' : 'grid', directed: false, spacingFactor: 1.4, padding: 40 } });
  cy.on('tap', 'node', e => { const detail = document.getElementById(`${elementId}-detail`); if (detail) detail.textContent = `${e.target.data('label')} · IP ${e.target.data('ip') || 'sin IP'}`; target.dispatchEvent(new CustomEvent('atlas-node-selected', { detail: e.target.data() })); });
  return cy;
};
