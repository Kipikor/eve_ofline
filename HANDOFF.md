# EVE Offline — аварийный handoff

Состояние зафиксировано перед переустановкой Windows 14 августа 2026 года.

## Где продолжать

- GitHub: `https://github.com/Kipikor/eve_ofline`
- рабочая ветка: `new-world`
- базовая ветка: `main`
- Unity: `6000.1.4f1` (`03270eb687c6`)
- основная сцена: `Assets/EveOffline/Scenes/Main.unity`
- основной код: `Assets/EveOffline/Scripts`
- описание реализованного игрового цикла: `README.md`

После клонирования открыть корень проекта в Unity Hub указанной версией. Папки `Library`, `Temp`, `Logs`, `Obj`, `Builds` и IDE-файлы намеренно не хранятся в Git и будут восстановлены Unity.

## Что реализовано

- полностью новый одиночный симулятор управления добывающим флотом вместо прежнего прототипа;
- готовые заблокированные комплекты кораблей: бесплатная Venture T0 и платные Ore/Ice/Gas/Booster T1/T2/ORE, встроенные shield-tank и боевые дроны; Mercoxit добывается обычными Ore-комплектами и сжимается общим рудным компрессором;
- навыки, очередь до 150 пунктов, перенос пунктов вверх/вниз, планы по корпусам и грейдам, Small/Large Skill Injectors;
- добыча отдельными модулями, автоцель по оставшейся кубатуре, автовыгрузка, визуальный отварп `5+10+5`, NPC и их ETA;
- бесплатные глобальные Mining Foreman Bursts в порядке: цикл, дальность, critical+отходы; кристальный burst не ставится. Industrial Core не расходует Heavy Water;
- fleet compression у Porpoise/Orca/Rorqual;
- постоянный мир добычи и save schema v14: ежедневное пополнение статических белтов в `11:00 UTC`, отдельный жизненный цикл аномалий, грейды руды;
- официальный каталог The Forge: 73 highsec-системы и 677 статических белтов, включая NPC-free белты уровня 0.9;
- глобальный автомаршрут с сохраняемой нижней границей security. Ручной прилёт задаёт границу, автоматика обходит только этот уровень и выше, сохраняет точный состав флота, HP и трюмы, затем возобновляет core/bursts;
- офлайн-догон: обучение, событийные границы добычи/перемещений, разгрузка, автомаршрут, NPC, bursts/core и world lifecycle. Автопродажи нет; ISK офлайн не меняется. Корабельный догон ограничен 30 сутками, а навыки, downtime и просроченные попытки редких аномалий догоняются до текущего времени с защитными пределами;
- компактный список направлений: одна строка на систему, security, число доступных белтов/аномалий и наличие NPC.

Подробности и игровые допущения находятся в `README.md`.

## Важный статус проверки

Последний крупный слой — 677 белтов, security-floor routing и офлайн-симуляция — проверялся только статически (поиск API, Roslyn syntax, invariants и smoke-код). После последних изменений Unity, полноценная сборка и smoke suite **не запускались**, потому что компьютер стал нестабилен. Поэтому первым действием после восстановления окружения нужно проверить компиляцию и прогнать smoke suite.

Рекомендуемый безопасный порядок:

1. Открыть проект в Unity `6000.1.4f1` и дождаться окончания импорта/компиляции.
2. Проверить Console на C# errors.
3. Создать/обновить сцену через `EVE Offline/Create Main Scene` при необходимости.
4. Запустить `EveOfflineBuild.RunSmokeTests` через Unity `-executeMethod` и убедиться, что лог содержит `EVE_OFFLINE_SMOKE_OK`.
5. Проверить вручную: загрузка существующего save, ночной офлайн-догон, переход через `11:00 UTC`, автомаршрут между security tiers, отсутствие автопродажи, NPC-free 0.9 и один опасный белт.
6. Только после этого делать Windows build через `EveOfflineBuild.BuildWindows`.

## Личный игровой save

Save намеренно **не добавлен в публичный GitHub-репозиторий**. Перед переустановкой Windows отдельно скопировать файл:

`C:\Users\korki\AppData\LocalLow\Tulia Works\EVE Offline - Mining Command\eve-offline-mining-command-v5.json`

На момент handoff файл существовал, размер около 1.02 MiB. После установки игры его нужно вернуть в тот же каталог. Не заменять его стартовым save: миграции в коде поддерживают версии 5–14.

## Технические ориентиры

- save/schema/migration: `Assets/EveOffline/Scripts/Core/SaveModels.cs`, `SaveService.cs`;
- хронологический офлайн-догон: `OfflineSimulationService.cs`;
- persistent sites, downtime и глобальный selector: `MiningSiteService.cs`;
- перелёты и security floor: `TravelService.cs`;
- добыча, NPC, bursts/core, warp: `OperationService.cs`;
- официальный highsec-каталог: `HighSecBeltCatalog.cs`;
- UI/runtime: `Assets/EveOffline/Scripts/Runtime/EveOfflineRuntime.cs`;
- static smoke suite/build entrypoints: `Assets/EveOffline/Scripts/Editor/EveOfflineBuild.cs`.

## Не потерять при восстановлении

- Продолжать именно с ветки `new-world`, а не с устаревших `main`/`master`.
- Старые удалённые Assets относятся к прежнему прототипу; массовые удаления в коммите намеренные.
- `smoke.log` в корне — старый локальный лог от 13 августа и намеренно исключён из Git: он не подтверждает состояние текущего кода.
- Не коммитить `Library`, `Temp`, `Logs`, `Obj`, `Builds`, `UserSettings` и локальный save.
