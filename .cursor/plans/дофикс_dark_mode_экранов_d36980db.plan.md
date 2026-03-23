---
name: Дофикс dark mode экранов
overview: Точечно исправить остаточные проблемы тёмной темы на вкладках Участники/Стадии и экране Сетка Плей-офф, используя существующие theme-aware ресурсы и минимально добавив новые токены для playoff-акцентов.
todos:
  - id: fix-dark-tournamentdetails-stages
    content: Исправить dark mode для вкладки Стадии в TournamentDetailsPage (CardFrameStyle + secondary text color)
    status: completed
  - id: fix-dark-tournamentdetails-participants
    content: Исправить dark mode для вкладки Участники в TournamentDetailsPage (CardFrameStyle + secondary text color)
    status: completed
  - id: fix-dark-playoff-bracket
    content: Заменить hardcoded цвета в PlayoffBracketPage на theme-aware ресурсы, включая selected-state чипов и карточки
    status: completed
  - id: verify-dark-regression-three-screens
    content: Проверить тёмную/светлую тему на Стадии, Участники и Сетка Плей-офф
    status: completed
isProject: false
---

# Дофикс тёмной темы на 3 экранах

## Что осталось сломанным

- На вкладках `Стадии` и `Участники` в [D:/Materials/Projects/HockeyTournamentTracker/Presentation/Views/TournamentDetailsPage.xaml](D:/Materials/Projects/HockeyTournamentTracker/Presentation/Views/TournamentDetailsPage.xaml) карточки и вторичный текст частично используют фиксированные/дефолтные светлые цвета.
- На экране сетки в [D:/Materials/Projects/HockeyTournamentTracker/Presentation/Views/PlayoffBracketPage.xaml](D:/Materials/Projects/HockeyTournamentTracker/Presentation/Views/PlayoffBracketPage.xaml) остались жёсткие `White`, `Gray` и `#...` (чипы раундов, карточки серий, подписи, коннекторы, карточки в брекете).

## План изменений

- Обновить вкладку `Стадии` в `TournamentDetailsPage`:
  - применить `Style="{StaticResource CardFrameStyle}"` к карточке стадии;
  - заменить `TextColor="Gray"` у вторичного текста стадии на `AppThemeBinding` с `SecondaryTextLight/SecondaryTextDark`.
- Обновить вкладку `Участники` в `TournamentDetailsPage`:
  - применить `CardFrameStyle` к карточке участника;
  - заменить `TextColor="Gray"` у `ShortName` на `SecondaryTextLight/SecondaryTextDark`.
- Дофиксить `PlayoffBracketPage`:
  - заменить фиксированные цвета карточек/плашек/подписей/коннекторов на `AppThemeBinding` + существующие ресурсы (`CardSurface*`, `CardBorder*`, `CardSurfaceMuted*`, `SecondaryText*`);
  - привести selected-состояние чипа раунда и заголовки колонок к theme-aware акцентам (через ресурсы, чтобы не оставлять светлые оттенки в dark).
- При необходимости добавить недостающие playoff-акцентные токены в [D:/Materials/Projects/HockeyTournamentTracker/Resources/Styles/Colors.xaml](D:/Materials/Projects/HockeyTournamentTracker/Resources/Styles/Colors.xaml) и использовать их в `PlayoffBracketPage.xaml`.

## Проверка

- Переключить на тёмную тему и проверить:
  - вкладку `Стадии` (карточки и вторичный текст читаемы);
  - вкладку `Участники` (карточки не белые, текст контрастный);
  - `Сетка: Плей-офф` (чипы раундов, карточки серий/брекета, подписи и коннекторы без светлых артефактов).
- Быстрая регрессия в светлой теме: визуальная целостность карточек и выбранного раунда.

