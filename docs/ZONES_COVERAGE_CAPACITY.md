# Zones, coverage and capacity
NetworkZone tiene código único, estado, geografía y capacidad total/reservada/usada. CoverageCheck conserva estado y capacidad observada; ausencia de evidencia exige validación de campo.

Los estados de zona son Planned, Building, Active, CapacityLimited, Saturated y Retired. La API valida que la zona exista antes de asociar un servicio. La activación reserva el download Mbps del plan; cancelación libera la reserva y cambio de plan revierte si no hay capacidad.

CoverageCheck distingue Available, ProbablyAvailable, RequiresFieldValidation, NoCoverageConfirmed, CapacityLimited, Saturated y Planned. El texto de una dirección no basta para declarar cobertura.

Runtime demuestra distribución LAB por zona y el smoke REST demuestra altas/listado mediante `POST /api/operations/zones` y `GET /api/operations/zones`.
