---
name: stage-edit-and-details
overview: Упростить окно редактирования стадии и добавить отдельное окно стадии с матчами и таблицей только для выбранной стадии.
todos:
  - id: rename-stage-name-label
    content: Переименовать подпись поля Name в окне редактирования стадии на "Название стадии" и убрать UI для поля "Порядок"
    status: completed
  - id: create-stage-details-page
    content: Создать страницу StageDetailsPage с шапкой стадии, списком матчей и таблицей для выбранной стадии
    status: completed
  - id: wire-stage-tap-navigation
    content: Сделать элементы списка стадий кликабельными и настроить навигацию из TournamentDetailsPage на StageDetailsPage
    status: completed
  - id: integrate-edit-and-match-actions
    content: Добавить из окна стадии действия по редактированию стадии и добавлению/редактированию матчей
    status: completed
isProject: false
---

## Цели

- **Упростить добавление/редактирование стадии**: убрать поле порядка и переименовать поле названия.
- **Сделать стадии кликабельными**: при выборе стадии открывается отдельная страница с матчами и турнирной таблицей только этой стадии, а также с возможностью редактировать саму стадию и добавлять/редактировать матчи.

## Изменения по пункту 1 (окно добавления стадии)

- **Переименовать подпись поля `Name`**
  - В [Presentation/Views/StageEditPage.xaml](Presentation/Views/StageEditPage.xaml) заменить текст метки над полем `Name`:
    - Было:
      - `AppResources.GroupName` ("Группа").
    - Стало:
      - Либо новый ресурс локализации `StageName`, либо временно явная строка "Название стадии" (если локализация пока не критична).
  - Сохранить биндинг `Entry Text="{Binding Name}"`, т.к. в доменной модели `Stage` уже есть свойство `Name`.
- **Скрыть/удалить поле "Порядок" из UI**
  - В [Presentation/Views/StageEditPage.xaml](Presentation/Views/StageEditPage.xaml) удалить/закомментировать:
    - `Label Text="Порядок"`.
    - `Entry Text="{Binding Order}" Keyboard="Numeric"`.
  - Логику вычисления `Order` оставить внутри `StageEditViewModel.LoadAsync`:
    - При создании новой стадии значение `Order` уже автоматически вычисляется как `max(Order) + 1`.
    - В `SaveAsync` свойство `Order` продолжит сохраняться, но пользователь больше его не редактирует — порядок стадий будет внутренним, по умолчанию.
- **Проверить сохранение стадий**
  - В [Presentation/ViewModels/StageEditViewModel.cs](Presentation/ViewModels/StageEditViewModel.cs) убедиться, что:
    - `SaveAsync` не полагается на пользовательский ввод `Order`, а работает с уже установленным значением.
    - При редактировании существующей стадии `Order` загружается и сохраняется как есть (скрытое поле), без изменения со стороны пользователя.

## Изменения по пункту 2 (кликабельные стадии и отдельное окно стадии)

### 2.1. Страница деталей стадии

- **Создать новую страницу деталей стадии**
  - Добавить XAML-страницу `StageDetailsPage` в `Presentation/Views`.
  - Структура:
    - Шапка со следующей информацией:
      - Название стадии (`Stage.Name`).
      - Тип стадии (отображение по `Stage.StageType`: "Швейцарская система" / "Плей-офф").
      - Необязательный текстовый индикатор порядка (если решим его показывать позже, по `Stage.Order`).
    - Кнопки управления:
      - **Редактировать стадию** → переход на `StageEditPage` с параметрами `TournamentId` и `StageId`.
      - **Добавить матч** → переход на `MatchEditPage` с параметрами `TournamentId` и `StageId`.
    - Блок "Матчи стадии":
      - `CollectionView` по аналогии с блоком `StageMatches` из вкладки "Стадии" на [Presentation/Views/TournamentDetailsPage.xaml](Presentation/Views/TournamentDetailsPage.xaml): список матчей с командами, датой/временем, статусом, счётом.
      - Кнопки "Начать" / "Завершить" матч и переход к редактированию матча, аналогичные уже существующим обработчикам `OnStartMatchClicked` и `OnFinishMatchClicked`.
    - Блок "Турнирная таблица стадии":
      - `CollectionView` по аналогии с `StandingsByGroupForStage` во вкладке "Стадии".
- **ViewModel для страницы стадии**
  - Вариант 1 (предпочтительный, с переиспользованием логики):
    - Создать отдельный `StageDetailsViewModel` в `Presentation/ViewModels`.
    - Включить в него:
      - Свойства:
        - `Tournament` (или только `TournamentId`).
        - `Stage` (текущая стадия).
        - `ObservableCollection<MatchRow> StageMatches`.
        - `ObservableCollection<StandingGroup> StandingsByGroupForStage`.
      - Методы:
        - `LoadAsync(Guid tournamentId, Guid stageId)` —
          - Загрузить турнир и стадию через соответствующие репозитории.
          - Загрузить матчи турнира и отфильтровать по `StageId`.
          - Получить команды турнира и сформировать `MatchRow` так же, как в `TournamentDetailsViewModel.CreateMatchRow` / `RefreshStageMatches`.
          - Построить таблицу через тот же метод `BuildStandingsByGroupFromMatches` (вынести его в общий сервис/хелпер, если сейчас он приватный в `TournamentDetailsViewModel`).
      - Команды/методы для UI:
        - `Task SetMatchStatusAsync(Guid matchId, MatchStatus status)` — по аналогии с `TournamentDetailsViewModel.SetMatchStatusAsync`.
    - По возможности **вынести общую логику**:
      - Методы вроде `BuildStandingsByGroupFromMatches` и создания `MatchRow` вынести в отдельный сервис/хелпер (например, `StageStatisticsService`), чтобы не дублировать код между `TournamentDetailsViewModel` и `StageDetailsViewModel`.
  - Вариант 2 (быстрый, но менее чистый):
    - Переиспользовать существующий `TournamentDetailsViewModel`, пробрасывая в него `SelectedStage` через параметры навигации, и показывать на новой странице только часть его UI.
    - Этот вариант можно рассмотреть, но в текущем плане делаем ставку на отдельный VM для большей ясности.
- **Регистрация маршрута и приём параметров**
  - В [AppShell.xaml.cs](AppShell.xaml.cs) зарегистрировать маршрут:
    - `Routing.RegisterRoute(nameof(StageDetailsPage), typeof(StageDetailsPage));`
  - В коде-behind `StageDetailsPage.xaml.cs` реализовать интерфейс `IQueryAttributable` для получения параметров:

```csharp
public async void ApplyQueryAttributes(IDictionary<string, object> query)
{
    if (query.TryGetValue("TournamentId", out var tVal) &&
        query.TryGetValue("StageId", out var sVal) &&
        tVal is string tStr && sVal is string sStr &&
        Guid.TryParse(tStr, out var tournamentId) &&
        Guid.TryParse(sStr, out var stageId))
    {
        await _viewModel.LoadAsync(tournamentId, stageId);
    }
}
```

### 2.2. Кликабельность стадий в списке на вкладке "Стадии"

- **Сделать элементы списка стадий активными**
  - В блоке списка стадий на [Presentation/Views/TournamentDetailsPage.xaml](Presentation/Views/TournamentDetailsPage.xaml) добавить жест `TapGestureRecognizer` к элементу `DataTemplate` (корневому `Grid` или `StackLayout`) для стадии:
    - При тапе по элементу вызывать обработчик в code-behind или команду в VM.
- **Обработчик клика по стадии**
  - В [Presentation/Views/TournamentDetailsPage.xaml.cs](Presentation/Views/TournamentDetailsPage.xaml.cs) добавить метод, который:
    - Получает объект `Stage` из `BindingContext` элемента.
    - Выполняет навигацию на новую страницу:

```csharp
private async void OnStageTapped(object? sender, TappedEventArgs e)
{
    if ((sender as BindableObject)?.BindingContext is not Stage stage || _viewModel.Tournament is null)
        return;

    await Shell.Current.GoToAsync($"{nameof(StageDetailsPage)}?TournamentId={_viewModel.Tournament.Id}&StageId={stage.Id}");
}
```

- **Сохранить текущую функциональность выбора стадии на вкладке**
  - Если текущий `SelectionChanged="OnStageSelectionChanged"` уже устанавливает `SelectedStage` и обновляет `StageMatches`, оставить это поведение для внутривкладочного просмотра.
  - Новая навигация по тапу будет дополнять текущую: пользователь может либо просматривать матчи стадии прямо во вкладке, либо уйти на отдельное окно стадии для более фокусного просмотра.

### 2.3. Взаимодействие окна стадии с редактированием матчей и самой стадии

- **Кнопка "Редактировать стадию" на странице стадии**
  - В `StageDetailsPage.xaml.cs` добавить обработчик:

```csharp
private async void OnEditStageClicked(object? sender, EventArgs e)
{
    if (_viewModel.Stage is null) return;
    await Shell.Current.GoToAsync($"{nameof(StageEditPage)}?TournamentId={_viewModel.Stage.TournamentId}&StageId={_viewModel.Stage.Id}");
}
```

- **Кнопка "Добавить матч" на странице стадии**
  - Аналогично существующему `OnAddMatchInStageClicked` в `TournamentDetailsPage.xaml.cs`, но используя текущую стадию из VM страницы стадии:

```csharp
private async void OnAddMatchClicked(object? sender, EventArgs e)
{
    if (_viewModel.Stage is null) return;
    await Shell.Current.GoToAsync($"{nameof(MatchEditPage)}?TournamentId={_viewModel.Stage.TournamentId}&StageId={_viewModel.Stage.Id}");
}
```

- **Старт/финиш и редактирование матча из окна стадии**
  - В `StageDetailsPage` использовать те же подходы, что и во вкладке "Стадии" `TournamentDetailsPage`:
    - Кнопка "Start match" → вызов `SetMatchStatusAsync(matchId, MatchStatus.InProgress)` в `StageDetailsViewModel`.
    - Кнопка "Finish match" → переход на `MatchEditPage` с `TournamentId` и `MatchId`.

## Итог

- UI редактирования стадии станет проще и понятнее: пользователь видит только "Название стадии" и "Тип стадии", без лишнего поля порядка.
- Список стадий во вкладке "Стадии" станет кликабельным: по тапу открывается отдельная страница стадии с её матчами и таблицей.
- Новая страница стадии будет давать фокусный обзор по конкретной стадии (шапка, тип, таблица, список матчей, операции с матчами и редактирование самой стадии), при этом переиспользуя уже существующую доменную модель и репозитории, чтобы минимизировать дублирование логики.

