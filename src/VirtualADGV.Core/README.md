# VirtualADGV.Core 2.0.0

UI-independent building blocks for virtual grids. Targets .NET Framework 4.6.2
and .NET 8, with no runtime package dependencies. MIT licensed.

- `VirtualADGV.Data`: asynchronous table sources, 64-bit ordinals, paged lists,
  placeholder rows and `PageLoaded` notifications on the captured synchronization context.
- `VirtualADGV.Grid`: `MinimapModel`, `RowSearch`, `OrdinalSelection` and `VirtualGridTexts`.
- `RowSearch.InMemoryRowLimit` is 200,000; adapters use it to decide whether to scan a source.
- Minimap drawing and the 100 ms deferred-scroll timer belong to the UI adapter.
- WPF 2.0.0 forwards the existing public data types to this assembly.

## Türkçe

Sanal tablolar için arayüzden bağımsız çekirdek. .NET Framework 4.6.2 ve .NET 8
hedefleri; çalışma zamanında ek paket bağımlılığı yok. MIT lisanslı.

Veri kaynağı sözleşmeleri, sayfalı liste, 64 bit sıra seçimi, mini harita modeli,
bellek içi arama ve ortak metinler içerir. `PageLoaded`, varsa kurulum sırasında
yakalanan `SynchronizationContext` üzerinden bildirilir. Çizim ve 100 ms
bekleme zamanlayıcısı arayüz tarafında kalır. WPF 2.0.0 mevcut veri tiplerini
`TypeForwardedTo` ile bu derlemeye yönlendirir; ad alanları korunur.
