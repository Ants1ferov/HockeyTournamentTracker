---
name: stage-groups-fix
overview: "Переведем группы в режим “внутри стадии”: данные, UI и валидации матчей будут использовать группировку на уровне `StageTeams`. Параллельно починим перемещение/добавление выбранных команд и увеличим зазор между колонками “О” и “И” в турнирной таблице."
todos:
  - id: db-stage-groups
    content: Добавить `StageGroups` + `GroupId` в `StageTeams`, миграцию SQLite, обновить `StageTeamRepository` и добавить `IStageGroupRepository`.
    status: completed
  - id: vm-stage-details-groups
    content: Обновить `StageDetailsViewModel`/`StageDetailsPage.xaml` так, чтобы колонки и standings строились по группам текущей `Stage`, а move/rename/delete работали с `StageTeams.GroupId`.
    status: completed
  - id: vm-tournament-standings-stage
    content: Обновить `TournamentDetailsViewModel` (вкладка стадий) для построения standings по `StageGroups` и `StageTeams.GroupId`.
    status: completed
  - id: match-edit-stage-group-filter
    content: Обновить `MatchEditViewModel` чтобы picker и валидация cross-group matches использовали группировку внутри стадии, а не `Team.GroupId`.
    status: completed
  - id: ux-add-selected-and-move
    content: "Починить UX `AvailableTeams`/Move: сделать выбор команд для “Добавить выбранные” стабильным и убрать причины, по которым move не срабатывает при тапе/зажиме."
    status: completed
  - id: table-column-spacing
    content: Увеличить `ColumnSpacing`/ширины в таблицах `TournamentDetailsPage.xaml` и `StageDetailsPage.xaml` между колонками “О” и “И”.
    status: completed
isProject: false
---

## Что будет сделано

- Сделаем группы, редактирование групп и присвоение команд по группам привязанными к конкретной `Stage`.
- Починим UX в `StageDetailsPage`: “Добавить выбранные” начнет работать, а перемещение выбранных команд в группу будет стабильно выполняться.
- Увеличим расстояние между колонками “О” и “И” в таблице (в обоих местах, где таблица строится).

## Ключевая проблема в текущем коде

Сейчас группировка команд для таблицы и колонок берется из `Team.GroupId`, а список групп берется из `Tournament.Rules.Groups`.

Пример текущей логики:

- В `Presentation/ViewModels/StageDetailsViewModel.cs` колонки групп строятся из `tournament.Rules.Groups`, а команды попадают в колонку по `t.GroupId == group.Id`.
- В `Presentation/ViewModels/TournamentDetailsViewModel.cs` группировка для standings делается по `teams.Where(t => t.GroupId == group.Id)`.

Чтобы группы были “внутри стадии”, придется переносить хранение групп в пределах `StageTeams` (или в связанную таблицу `StageGroups`) и использовать их в построении колонок/standings.

## Реализация (по шагам)

### 1) Модель данных: StageGroups + групповой Id на уровне StageTeams

1. Добавить новую сущность/таблицу:
  - `Data/Entities.cs`: `StageGroupEntity` (таблица `StageGroups`), поля: `Id`, `StageId`, `Name`, `Order`.
  - `Data/LocalDatabase.cs`: добавить `CreateTableAsync<StageGroupEntity>()`.
2. Расширить `StageTeams`:
  - `Data/Entities.cs`: добавить nullable поле `GroupId` в `StageTeamEntity` (например `Guid? GroupId`).
  - `Data/LocalDatabase.cs`: миграция `ALTER TABLE StageTeams ADD COLUMN GroupId TEXT` (через try/catch как сделано для `Teams`).
3. Обновить `IStageTeamRepository` / `StageTeamRepository`:
  - `GetTeamIdsByStageAsync` оставить как есть (для фильтра матчей/выбора команд).
  - Добавить методы для получения связки “команда -> группа в рамках стадии” и изменения группы для команды/списка команд.
  - Пример требований к API репозитория:
    - получить `GroupId` для команд в рамках `StageId` (нужно и для UI колонок, и для построения standings);
    - атомарно обновить `GroupId` у `StageTeams` для списка teamIds.
4. Добавить `Data/IStageGroupRepository` / `Data/StageGroupRepository`:
  - `GetByStageAsync(stageId)`
  - `AddAsync(stageId, name)`
  - `RenameAsync(stageId, groupId, newName)`
  - `DeleteAsync(stageId, groupId)` (и обнуление `StageTeams.GroupId`, чтобы команды уходили в “Без группы”).
5. `MauiProgram.cs`: зарегистрировать новый репозиторий.

### 2) UI и VM: StageDetailsPage управляет группами и колонками внутри стадии

1. `Presentation/ViewModels/StageDetailsViewModel.cs`:
  - Добавить загрузку `StageGroups` текущей `Stage` через `IStageGroupRepository`.
  - Заменить `BuildStageGroupColumns(...)`, чтобы колонки строились по `StageGroups`, а команды фильтровались по `StageTeams.GroupId`.
  - Заменить `MoveSelectedTeamsToGroupAsync(...)`, чтобы обновлялся `StageTeamEntity.GroupId` (а не `Team.GroupId`).
  - Обновить `AddGroupAsync(...)` и добавить команды редактирования групп:
    - `RenameStageGroupAsync(groupId, newName)`
    - `DeleteStageGroupAsync(groupId)`
2. “Fallback” для старых турниров:
  - Если `StageGroups` пустая, а в `Tournament.Rules.Groups` есть группы — создать `StageGroups` копированием.
  - Если `StageTeams.GroupId` еще не заполнен, использовать текущее `Team.GroupId` (глобальное) как стартовую инициализацию для `StageTeams.GroupId`.
3. `Presentation/Views/StageDetailsPage.xaml`:
  - Добавить секцию списка групп стадии с кнопками rename/delete.
  - Покрыть сценарий “случайно задал не то наименование”: rename через `DisplayPromptAsync`.
4. Починка перемещения команд (пункты 1 и 2 из сообщения пользователя):
  - Перейти от текущего хрупкого обработчика тапов к передаче параметров через `Command`/`CommandParameter` (чтобы move гарантированно работал).
  - Оставить и/или добавить режим выделения:
    - выделяем команды (тап/клик по команде);
    - цель задается тапом по заголовку колонки или отдельной кнопкой “Переместить выбранные” с выбором колонки.

### 3) Турнирная таблица по стадии: `TournamentDetailsViewModel` должен учитывать StageGroups

1. `Presentation/ViewModels/TournamentDetailsViewModel.cs`:
  - В `RefreshStageMatches()` и `BuildStandingsByGroupFromMatches(...)` заменить логику группировки:
    - раньше: `teams.Where(t => t.GroupId == group.Id)` + `Tournament.Rules.Groups`;
    - после: `StageGroups` текущей стадии + группировка по `StageTeams.GroupId`.
2. В конструктор VM добавить зависимости:
  - `IStageTeamRepository` и `IStageGroupRepository`.

### 4) MatchEditViewModel: фильтрация выбора команд по группам внутри стадии

1. `Presentation/ViewModels/MatchEditViewModel.cs`:
  - В `RefreshAwayTeamOptions()` и `SaveAsync()` заменить сравнение `HomeTeam.GroupId`/`AwayTeam.GroupId` на сравнение групп “в рамках конкретной стадии” (через новые методы `IStageTeamRepository`).
  - Это уберет “слишком широкие” pickers и запреты, когда группы должны быть разными по стадиям.

### 5) Кнопка “Добавить выбранные” и выбор команд из списка (пункт 4)

1. `Presentation/Views/StageDetailsPage.xaml` + `StageDetailsPage.xaml.cs`:
  - Проверить/исправить механизм выбора в `AvailableTeams`:
    - сейчас используется `CollectionView SelectionMode="Multiple"` + `SelectionChanged` и `_selectedAddTeamIds`.
  - Если на целевой платформе SelectionChanged “не поднимается” (или визуально неочевидно), заменить на управляемое выделение через `TapGestureRecognizer` внутри ItemTemplate и подсветку выбранных.
2. Убедиться, что `OnAddSelectedTeamsClicked` вызывается с корректным набором teamIds и после добавления список и колонки перезагружаются.

### 6) Исправление расстояния между колонками “О” и “И” (пункт 5)

1. Изменить `ColumnSpacing` или ширины колонок в:
  - `Presentation/Views/TournamentDetailsPage.xaml` (grid заголовков + grid строк в таблице стадии)
  - `Presentation/Views/StageDetailsPage.xaml` (аналогично)

Сейчас обе таблицы используют фиксированное `ColumnSpacing="2"` и ширины колонок “О”/“И” одинаковые, поэтому визуальный зазор получается слишком маленьким.

## Ожидаемый результат

- Группы создаются/редактируются только внутри конкретной стадии.
- Команды можно перемещать между колонками внутри стадии (тапом по колоне или кнопкой; затем при необходимости — расширить до перетаскивания).
- “Добавить выбранные” работает и добавляет выбранные команды в стадию.
- Расстояние между “О” (очками) и “И” (играми) в таблице визуально стало больше.

## Минимальная проверка (ручная)

- Создать турнир с 1-2 командами и 1 стадией.
- Внутри стадии создать/переименовать/удалить группы.
- Добавить команды в стадию (Add all / Add selected).
- Переместить команды из колонки в колонку и убедиться, что таблица пересчитывается по текущей стадии.
- Открыть матч из стадии: в picker доступны только команды этой стадии, а валидация cross-group matches соответствует выбранным группам стадии.
- Проверить на экране деталей турнира/стадии: “О” и “И” визуально разделены.

