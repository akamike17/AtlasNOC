# Domain model
Customer, CustomerService, ServicePlan, BillingAccount, InventoryAsset, NetworkZone y NetworkSite son conceptos separados. Los CustomerService conservan identidad independiente y pueden coexistir por cliente.

Cada servicio conserva plan, domicilio, estado, provisioning y `ZoneId`. BillingAccount referencia al Customer, no al servicio, y BillingEntry conserva monto decimal, UTC, periodo e idempotency key. Cancelar un servicio no borra customer ni otros servicios.

InventoryAsset conserva lifecycle y `RowVersion` para que una segunda asignación concurrente produzca conflicto controlado. NetworkZone registra código único, estado y capacidad reservada/usable.

La invariantes se aplican antes de persistir: ServiceCode es único, la zona debe existir, un asset no puede tener dos servicios activos y las reservas de capacidad deben coincidir con los servicios activos. BillingEntry usa monto decimal, UTC y clave idempotente única.

La cancelación libera la reserva de zona y conserva Customer, BillingAccount, historial y otros CustomerService. Un pago o crédito crea evidencia contable y de soporte; no convierte una acción física no soportada en éxito.

La evidencia se ejecuta con las pruebas unitarias y de integración MySQL, especialmente `Concurrent_asset_assignment_has_one_winner` y los flujos de billing. Un error de FK, unique o concurrencia debe devolver conflicto/validación y no dejar una mutación a medias.
