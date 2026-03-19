---
name: stage-groups-teams
overview: Добавить хранение состава стадии (StageTeams), ограничить выбор команд в матчах командами стадии, внедрить UI управления группами и визуального перемещения команд в группы внутри StageDetails, убрать выбор группы из создания команды, а также добить буллиты в счёте и устранить дубли стадий при возврате на страницу турнира.
todos:
  - id: stage-team-repo-and-db
    content: Добавить модель/таблицу StageTeams (Data/Entities.cs + Data/LocalDatabase.cs) и репозиторий IStageTeamRepository/StageTeamRepository для чтения/добавления команд в стадию.
    status: completed
  - id: di-stage-team-repo
    content: Зарегистрировать StageTeamRepository в MauiProgram и обновить конструкторы StageDetailsViewModel/MatchEditViewModel для инъекции IStageTeamRepository.
    status: completed
  - id: team-edit-remove-group-picker
    content: Удалить Picker выбора группы из TeamEditPage и убрать/упростить соответствующую логику в TeamEditViewModel (новые команды без групп, существующие сохраняют текущий GroupId).
    status: completed
  - id: stage-details-teams-and-groups-ui
    content: "Расширить StageDetailsPage/XAML и StageDetailsViewModel: UI Add all/Add selected для добавления команд в стадию, кнопка добавления групп, колонки по группам и обработчики перемещения выбранных команд в группу."
    status: completed
  - id: restrict-match-team-pickers-to-stage
    content: В MatchEditViewModel ограничить Teams/AwayTeamOptions командами из состава стадии; в SaveAsync валидировать что Home/Away входят в StageTeams (с fallback по существующим матчам, если StageTeams пустая).
    status: completed
  - id: bullits-period-scores-and-scoretext
    content: Добавить PeriodType.Shootout в MatchEditViewModel.SaveAsync при OutcomeType.Shootout и обновить BuildScoreText в TournamentDetailsViewModel и StageDetailsViewModel, чтобы не дублировать буллиты/овертайм.
    status: completed
  - id: fix-duplicate-stages-on-back
    content: Добавить защиту от параллельных LoadAsync в TournamentDetailsViewModel (например, SemaphoreSlim), чтобы при возврате из StageDetails не возникали дубли в коллекции Stages.
    status: completed
isProject: false
---

## Цели

1. Убрать из добавления команд выбор группы.
2. Внутри окна стадии добавить:
  - кнопку добавления групп,
  - управление составом стадии: добавить все или выбрать команды и добавить их в стадию,
  - визуальную возможность добавлять/перемещать команды в группы (колонки по группам).
3. Ограничить отображение/выбор команд в конкретной стадии только командами, добавленными в эту стадию.
4. Довести UI/модель матча до корректного отображения буллитов.
5. Исправить баг с дублированием стадий при возврате из окна стадии в окно турнира.

## Архитектурные изменения (схема)

```mermaid
flowchart LR
  TournamentDetails["TournamentDetailsPage"] -->|"tap stage"| StageDetails["StageDetailsPage"]

  StageDetails -->|LoadAsync| StageTeamsRepo["IStageTeamRepository" (StageTeams)]
  StageDetails -->|Load standings| StatsService
  MatchEdit["MatchEditPage"] -->|StageId| MatchEditVM["MatchEditViewModel"] -->|Teams filter| StageTeamsRepo

  StageDetails -->|group add/save| TournamentRules["Tournament.Rules.Groups"]
  StageDetails -->|visual move to group| TeamRepo["ITeamRepository" -> Team.GroupId]
```



## Изменения по пунктам

### 1) Убрать указание групп при добавлении команд

- Изменить UI и VM страницы создания/редактирования команды:
  - `Presentation/Views/TeamEditPage.xaml`: удалить Label + `Picker` для группы.
  - `Presentation/ViewModels/TeamEditViewModel.cs`: убрать логику загрузки `Groups`, `SelectedGroup`, и сохранить `Team.GroupId` только из загруженной сущности (для существующих команд) или оставить `null` для новых.

Ключевое: при создании новой команды пользователь не выбирает группу, группа будет назначаться в UI стадии.

### 2) Хранить состав стадии (StageTeams) и ограничить команды в матчах

- Добавить новую сущность и репозиторий:
  - `Data/Entities.cs`: добавить `StageTeamEntity` (StageId, TeamId, PrimaryKey Id).
  - `Data/LocalDatabase.cs`: добавить `CreateTableAsync<StageTeamEntity>()`.
  - `Data/IStageTeamRepository` (новый файл) + `Data/StageTeamRepository` (новый файл):
    - получить teamIds по `StageId`,
    - добавить одну/несколько команд в стадию (с проверкой на дубликаты по (StageId,TeamId)).
- Интеграция:
  - `MauiProgram.cs`: зарегистрировать репозиторий и обновить DI для `StageDetailsViewModel` и `MatchEditViewModel`.
  - `Presentation/ViewModels/StageDetailsViewModel.cs`:
    - загрузить команды стадии: `StageTeams` -> `StageTeamsCollection`,
    - передавать в `BuildStandingsByGroupFromMatches(...)` только команды стадии, чтобы таблица не показывала «лишние» команды.
  - `Presentation/ViewModels/MatchEditViewModel.cs`:
    - при наличии `StageId` грузить `Teams` не из всех команд турнира, а только из состава стадии из `IStageTeamRepository`.
    - в `SaveAsync` валидировать, что выбранные Home/Away команды входят в состав стадии (иначе возвращать `false`).

Важно для обратной совместимости:

- если таблица `StageTeams` пока пустая, но уже есть матчи этой стадии, при первом открытии стадии использовать команды из матчей как «fallback» (и по желанию можно сохранить их в StageTeams, чтобы дальше работала строгая фильтрация).

### 3) Кнопка добавления групп внутри стадии

- `Presentation/Views/StageDetailsPage.xaml`:
  - добавить секцию «Группы» и кнопку `AppResources.AddGroup`.
- `Presentation/ViewModels/StageDetailsViewModel.cs`:
  - реализовать методы загрузки групп из `Tournament.Rules.Groups` и сохранение добавленной группы обратно через `ITournamentRepository.SaveAsync`.
  - UI по клику показывает prompt имени группы и вызывает `AddGroupAsync`.

### 4) Визуальное добавление/перемещение команд в группы внутри стадии

- `Presentation/Views/StageDetailsPage.xaml` (расширение UI):
  - добавить «колонки групп» по текущим `Tournament.Rules.Groups`.
  - добавить колонку для команд без группы (`Team.GroupId == null`).
  - отображать в колонках только команды, входящие в состав стадии.
- `Presentation/ViewModels/StageDetailsViewModel.cs`:
  - добавить состояние выбранных команд (из списка доступных/или из нераспределённых), например `SelectedTeamIds`.
  - при выборе целевой колонки группы (tap по заголовку/кнопке внутри колонки):
    - установить `Team.GroupId = targetGroupId` выбранным командам,
    - сохранить каждую команду через `ITeamRepository.SaveAsync` (или пакетно, если есть возможность).
  - обновить отображение колонок после сохранения.

### 5) Добавить кнопки состава стадии: добавить все / выбранные

- `Presentation/Views/StageDetailsPage.xaml`:
  - добавить UI список «Доступные команды» (все команды турнира минус команды стадии):
    - `CollectionView` с `SelectionMode="Multiple"`.
  - кнопки:
    - «Добавить все в стадию»
    - «Добавить выбранные в стадию»
- `StageDetailsViewModel`:
  - реализовать методы `AddAllTeamsToStageAsync()` и `AddSelectedTeamsToStageAsync(teamIds)`.
  - после добавления перезагрузить состав стадии и пересчитать standings/колонки.

### 6) Буллиты в матчах (и отображение корректно в счёте)

- `Presentation/ViewModels/MatchEditViewModel.cs`:
  - в `SaveAsync` при `OutcomeType == OutcomeType.Shootout` добавлять в `match.PeriodScores` запись `PeriodType.Shootout` (как 4-й период при отсутствии OT или как 5-й при наличии OT).
- `Presentation/ViewModels/TournamentDetailsViewModel.cs` и `Presentation/ViewModels/StageDetailsViewModel.cs`:
  - обновить `BuildScoreText(...)`, чтобы буллит/овертайм не дублировались одновременно и в `periodPart`, и в `outcomeSuffix`.

### 7) Баг с дублированием стадий при возврате назад

- `Presentation/ViewModels/TournamentDetailsViewModel.cs`:
  - добавить защиту от параллельных `LoadAsync` вызовов (например, `SemaphoreSlim`), потому что `TournamentDetailsPage` использует `async void ApplyQueryAttributes` + `OnAppearing`, и они могут перекрываться.
  - результат: `Stages.Clear()` и повторная подгрузка выполняются последовательно.

## Что затронем (основные файлы)

- DB/данные: `Data/Entities.cs`, `Data/LocalDatabase.cs`, новые `Data/IStageTeamRepository.cs` и `Data/StageTeamRepository.cs`.
- DI: `MauiProgram.cs`.
- UI/VM: `Presentation/Views/TeamEditPage.xaml`, `Presentation/ViewModels/TeamEditViewModel.cs`, `Presentation/Views/StageDetailsPage.xaml`, `Presentation/Views/StageDetailsPage.xaml.cs`, `Presentation/ViewModels/StageDetailsViewModel.cs`, `Presentation/ViewModels/MatchEditViewModel.cs`, `Presentation/ViewModels/TournamentDetailsViewModel.cs`.
- Буллиты: `Presentation/ViewModels/MatchEditViewModel.cs`, `Presentation/ViewModels/TournamentDetailsViewModel.cs`, `Presentation/ViewModels/StageDetailsViewModel.cs`.

## Test plan (минимальный)

- Создать турнир, добавить несколько команд.
- Создать 1-2 стадии.
- Внутри стадии:
  - добавить группу(ы), добавить команды в стадию (Add all / Add selected), проверить колонки.
  - переместить команды в разные группы и проверить, что таблица стадии пересчиталась.
- Открыть матч из стадии:
  - убедиться, что в picker доступны только команды этой стадии.
- Буллиты:
  - выбрать «По буллитам», задать `ShootoutHome/ShootoutAway`, сохранить, проверить, что в отображении счёта присутствуют буллиты.
- Баг стадий:
  - открыть стадию, вернуться назад в турнир, проверить, что список стадий не дублируется.

