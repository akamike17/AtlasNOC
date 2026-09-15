# Laboratorio comercial 4×5

El fixture usa `ZONA-01` a `ZONA-04`, códigos únicos, servicios activos, capacidad reservada y BillingAccount asociado. La distribución ampliada comprobada es 100 clientes, 25 por zona, por lo que cubre el mínimo de cinco clientes por zona y permite probar escala.

Las invariantes son: Customer distinto de Service, ServiceCode único, zona válida, ningún Asset doblemente asignado y capacidad liberada al cancelar. El smoke REST conserva `POST /api/operations/zones` como contrato original.

Los datos se crean sólo con conexiones cuyo nombre contiene marcador LAB/test; no se cargan fixtures en producción ni se presentan estados simulados como hardware controlado.

Para reproducir el dataset ejecuta la suite Runtime y el smoke REST con `ATLASNOC_TEST_CONNECTION` configurada a una base LAB. El resultado esperado es cuatro códigos únicos, al menos cinco clientes por zona y reservas iguales a los servicios activos; una zona inexistente, código duplicado o doble asignación debe fallar.

La ejecución ampliada documentada usa 100 clientes y 25 por zona, mientras que el contrato mínimo es 20 clientes. El dataset sirve para demo y validación de invariantes, no para afirmar capacidad, latencia o demanda de clientes reales.
