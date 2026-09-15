# Customers, services and billing
La cuenta corriente usa cargos, pagos, créditos y saldo. BillingEntry admite clave idempotente persistida; Customer y CustomerService no se eliminan al cancelar un servicio. Los tickets de soporte reciben explícitamente el `CustomerServiceId` y la API rechaza una combinación servicio/cliente inconsistente.

Customer y CustomerService son entidades distintas: un cliente puede tener múltiples servicios, cada uno con plan, domicilio, zona, estado y provisioning. BillingAccount pertenece al cliente y se persiste después de la FK de Customer.

Los cargos se protegen por cuenta/tipo/clave y se procesan dentro de transacción serializable con execution strategy reintentable. PaymentReceipt conserva referencia única y las promesas aplican pagos sin reconectar un servicio con saldo insuficiente.

La prueba E2E ejecuta cargo, replay idempotente, consulta, promesa, pago, default y crédito con persistencia MySQL.
