# Domain model
Customer, CustomerService, ServicePlan, BillingAccount, InventoryAsset, NetworkZone y NetworkSite son conceptos separados. Los CustomerService conservan identidad independiente y pueden coexistir por cliente.

Cada servicio conserva plan, domicilio, estado, provisioning y `ZoneId`. BillingAccount referencia al Customer, no al servicio, y BillingEntry conserva monto decimal, UTC, periodo e idempotency key. Cancelar un servicio no borra customer ni otros servicios.

InventoryAsset conserva lifecycle y `RowVersion` para que una segunda asignación concurrente produzca conflicto controlado. NetworkZone registra código único, estado y capacidad reservada/usable.
