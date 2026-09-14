/* Presentación: el grafo llega del backend; el navegador no inventa relaciones. */
window.renderTopology = function (elementId, graph) {
  const target = document.getElementById(elementId);
  if (!target || !window.cytoscape || !graph) return;
  const nodes = (graph.nodes || []).map(n => ({ data: { id: n.id, label: `${n.hostname || n.managementIp}\n${n.managementIp}` } }));
  const edges = (graph.edges || []).map(e => ({ data: { id: e.id, source: e.source, target: e.target } }));
  const cy = cytoscape({ container: target, elements: [...nodes, ...edges], style: [
    { selector: 'node', style: { 'label': 'data(label)', 'background-color': '#198754', 'color': '#17202a', 'text-valign': 'bottom', 'text-margin-y': 8, 'font-size': 11 } },
    { selector: 'edge', style: { 'line-color': '#6c757d', 'width': 2, 'curve-style': 'bezier' } }
  ], layout: edges.length ? { name: 'breadthfirst', directed: false, spacingFactor: 1.4, padding: 40 } : { name: 'grid', fit: true, padding: 40 } });
  cy.on('tap', 'node', e => { target.dispatchEvent(new CustomEvent('atlas-node-selected', { detail: e.target.data() })); });
};
