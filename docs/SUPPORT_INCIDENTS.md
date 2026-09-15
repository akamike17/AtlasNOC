# Support and incidents
Tickets, interacciones, incidentes raíz, diagnóstico, visitas, rutas y créditos conservan actor, causa, evidencia y relación con el cliente afectado.

Un ticket nuevo exige `CustomerId` y `CustomerServiceId` del mismo cliente; la relación queda persistida en `SupportTickets` y evita atribuir soporte al servicio equivocado. Una interacción enlaza ticket e incidente raíz y conserva síntomas, diagnóstico, acciones, resultado y duración. El preview/create de crédito comprueba que el cliente tiene interacción de soporte que documenta la afectación.

Las visitas guardan ventana, tipo, minutos estimados/reales, traslado y resultado. Hardware o reparación no se marca completado por un driver sin capability.

El flujo de soporte es ticket → interacción → incidente raíz/diagnóstico → visita o crédito; cada registro conserva actor, timestamps y vínculo al CustomerService cuando aplica. La UI/API exige autenticación y antiforgery para mutaciones MVC.

Un crédito sólo puede previsualizarse o registrarse si existe evidencia de afectación del cliente. La respuesta esperada incluye el estado y la referencia del registro; si falta servicio, ticket o evidencia, la API devuelve validación y no inserta crédito.

El smoke REST y el E2E comercial verifican ticket, interacción, soporte ligado al servicio correcto y persistencia tras recarga en MySQL LAB. Operación real de guardias, SLA y equipo físico sigue siendo decisión humana.
