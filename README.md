# Movix для Jellyfin

[English](README.en.md) | [Русский](README.md)

[![CI](https://github.com/kexogg/jellyfin-plugin-movix/actions/workflows/ci.yml/badge.svg)](https://github.com/kexogg/jellyfin-plugin-movix/actions/workflows/ci.yml)

Плагин Jellyfin для подключения подписки Movix к Live TV. Плагин регистрирует в Jellyfin M3U-тюнер и гид XMLTV, а адрес
каждого канала получает непосредственно перед воспроизведением.

Это неофициальный проект для обеспечения совместимости. Он не связан с Дом.ру, Movix или Jellyfin. Используйте его
только со своей действующей подпиской и в соответствии с условиями предоставления услуги. Плагин не обходит DRM; каналы
без поддерживаемого HLS-ресурса недоступны.

## Требования

- Jellyfin Server 12.0.x
- Подписка Movix

## Установка из репозитория плагинов

1. Откройте в Jellyfin **Панель управления - Плагины - Репозитории**.
2. Добавьте репозиторий со следующим адресом:

   ```text
   https://raw.githubusercontent.com/kexogg/jellyfin-plugin-movix/master/manifest.json
   ```

3. Откройте **Каталог**, выберите **Movix connector for Jellyfin** и установите плагин.
4. Перезапустите Jellyfin.
5. Откройте **Панель управления - Плагины - Мои плагины - Movix connector for Jellyfin**.
6. Выполните вход и нажмите **Connect to Jellyfin Live TV**.
7. Проверьте воспроизведение канала в веб-клиенте Jellyfin. При необходимости отдельно проверьте воспроизведение по
   DLNA.

Обновления, опубликованные в этом репозитории, будут автоматически появляться в каталоге плагинов Jellyfin.

## Данные и безопасность

Плагин сохраняет только полученный токен и случайный идентификатор. Пароли, номера телефонов и SMS-коды не сохраняются.
При отключении плагин удаляет созданные им тюнер и источник программы передач, пытается отвязать устройство и удалит
сохраненную сессию.

Внутренние адреса плейлиста, программы передач и потоков принимают запросы только с loopback-интерфейса. Диагностические
данные доступны на `/Movix/Admin/Diagnostics`.

## Разработка

Установите .NET 10 SDK и выполните:

```bash
dotnet restore JellyfinMovix.slnx
dotnet format JellyfinMovix.slnx --verify-no-changes --severity info --no-restore
dotnet test JellyfinMovix.slnx --configuration Release --no-restore
```

## Публикация релиза

[build.yaml](build.yaml) служит источником метаданных и версии плагина. Для публикации:

1. Обновите `version` и `changelog` в `build.yaml` и зафиксируйте изменения в Git.
2. Создайте и опубликуйте GitHub Release с тегом `v<version>`, например `v0.1.0.0`.
3. Дождитесь завершения workflow **Release plugin**.
