/* Presentación: el grafo llega del backend; el navegador no inventa relaciones. */
window.renderTopology = function (elementId, graph) {
  const target = document.getElementById(elementId);
  if (!target) return null;
  target.replaceChildren();
  if (!window.cytoscape) { target.textContent = 'No se pudo cargar el motor de topología.'; return null; }
  const get = (o, camel, pascal) => o?.[camel] ?? o?.[pascal];
  const rawNodes = get(graph, 'nodes', 'Nodes') || [], rawEdges = get(graph, 'edges', 'Edges') || [];
  const nodes = rawNodes.map(n => { const id = String(get(n,'id','Id') ?? ''); const label = String(get(n,'label','Label') ?? get(n,'ip','Ip') ?? id); const ip = String(get(n,'ip','Ip') ?? ''); return { data: { id, label: ip && label !== ip ? `${label}\n${ip}` : label, ip, status: get(n,'status','Status'), vendor: get(n,'vendor','Vendor'), deviceType: get(n,'deviceType','DeviceType'), siteId: get(n,'siteId','SiteId') } }; }).filter(n => n.data.id && n.data.label);
  const ids = new Set(nodes.map(n => n.data.id));
  const edges = rawEdges.map(e => ({ data: { id: String(get(e,'id','Id') ?? ''), source: String(get(e,'source','Source') ?? ''), target: String(get(e,'target','Target') ?? ''), confirmed: get(e,'isConfirmed','IsConfirmed') } })).filter(e => e.data.id && ids.has(e.data.source) && ids.has(e.data.target));
  const count = document.getElementById(`${elementId}-count`); if (count) count.textContent = `${nodes.length} dispositivos / ${edges.length} enlaces`;
  const unlinked = document.getElementById(`${elementId}-unlinked`); if (unlinked) unlinked.textContent = `${get(graph,'unlinkedNodeCount','UnlinkedNodeCount') ?? 0} sin enlace`;
  const empty = document.getElementById(`${elementId}-empty`); if (empty) { empty.hidden = nodes.length !== 0; empty.textContent = 'No hay dispositivos inventariados para este filtro.'; }
  if (!nodes.length) return null;
  const colors = { 1: '#2ea043', 2: '#f85149', 3: '#d29922', 4: '#8b949e' };
  let cy;
  try { cy = cytoscape({ container: target, elements: [...nodes, ...edges], style: [
    { selector: 'node', style: { label: 'data(label)', 'background-color': el => colors[el.data('status')] || '#8b949e', color: '#17202a', 'text-valign': 'bottom', 'text-margin-y': 8, 'font-size': 11, 'border-width': 2, 'border-color': '#58a6ff' } },
    { selector: 'edge', style: { 'line-color': '#6c757d', width: 2, 'curve-style': 'bezier' } },
    { selector: 'edge[confirmed = false]', style: { 'line-style': 'dashed', 'line-color': '#8b949e' } }
  ], layout: { name: edges.length ? 'breadthfirst' : 'grid', directed: false, spacingFactor: 1.4, padding: 40 } }); } catch (error) { window.__topologyError = error.message; target.textContent = 'No se pudo dibujar la topología.'; return null; }
  target._atlasCy = cy;
  cy.on('tap', 'node', e => { const d=e.target.data(), detail=document.getElementById(`${elementId}-detail`); if (detail) { detail.textContent = ''; const name=document.createElement('strong'); name.textContent=d.label.split('\n')[0]; detail.append(name, ` · IP ${d.ip || 'sin IP'} · estado ${d.status ?? 'n/d'} · vendor ${d.vendor ?? 'n/d'} · tipo ${d.deviceType ?? 'n/d'} · sitio ${d.siteId || 'sin sitio'} `); const link=document.createElement('a'); link.href=`/devices/detail/${encodeURIComponent(d.id)}`; link.textContent='Ver detalle'; detail.append(link); } target.dispatchEvent(new CustomEvent('atlas-node-selected', { detail: d })); });
  return cy;
};

document.addEventListener('DOMContentLoaded', () => {
  const data = document.getElementById('atlas-topology-data');
  if (data && document.getElementById('topology-map')) window.renderTopology('topology-map', JSON.parse(data.textContent));
  const map = document.getElementById('cy');
  if (!map) return;
  const load = async () => { const site = document.getElementById('siteFilter')?.value; const r = await fetch('/api/topology/graph' + (site ? `?siteId=${encodeURIComponent(site)}` : '')); if (!r.ok) throw new Error(`HTTP ${r.status}`); const graph = await r.json(); window.renderTopology('cy', graph); const count = document.getElementById('cy-count'); if (count && graph.unlinkedNodeCount) count.textContent += ` · ${graph.unlinkedNodeCount} sin enlace`; };
  const fail = e => { const empty = document.getElementById('cy-empty'); if (empty) { empty.hidden = false; empty.textContent = `Error al cargar la topología: ${e.message}`; } };
  document.getElementById('refreshBtn')?.addEventListener('click', () => load().catch(fail));
  document.getElementById('siteFilter')?.addEventListener('change', () => load().catch(fail));
  load().catch(fail);
});
