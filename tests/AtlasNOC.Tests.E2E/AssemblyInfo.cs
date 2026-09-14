using Xunit;

// Las suites E2E comparten recursos externos (Playwright, puertos y MySQL de
// pruebas). Se serializan para que dos fixtures no migren/limpien sus bases a
// la vez; nunca se toca la base normal.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
