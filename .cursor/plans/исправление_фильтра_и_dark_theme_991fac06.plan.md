---
name: Исправление фильтра и dark theme
overview: Исправить неработающий фильтр матчей по названию команды и привести проблемные экраны к корректной тёмной теме через theme-aware ресурсы и стили.
todos:
  - id: fix-match-search-unicode
    content: Переписать фильтрацию по названию команды в MatchRepository на Unicode-корректную схему без зависимости от LOWER() SQLite
    status: completed
  - id: sync-stage-matches-query-flow
    content: Проверить и при необходимости подправить сбор/применение StageMatchQuery в StageMatchesPage.xaml.cs для стабильного обновления списка
    status: completed
  - id: add-theme-color-tokens
    content: Добавить/уточнить semantic-цвета для light/dark в Colors.xaml и подключить их в Styles.xaml
    status: completed
  - id: replace-hardcoded-colors
    content: Заменить фиксированные цвета и Gray на theme-aware ресурсы в MatchEditPage, StageMatchesPage, StageRosterPage, TeamsListPage и MainPage
    status: completed
  - id: verify-dark-light-regression
    content: Проверить сценарии поиска и UI-контраста в светлой/тёмной теме на ключевых экранах
    status: completed
isProject: false
---

# Исправить фильтр матчей и тёмную тему

## Цели

- Починить поиск матчей по названию команды (включая кириллицу).
- Убрать контрастные/цветовые артефакты в тёмной теме на экранах матчей, стадий и участников.

## Что исправить

- **Фильтр матчей:** в репозитории матчей заменить текущую стратегию `LOWER(...) + ToLowerInvariant()` на Unicode-безопасный вариант сравнения по подстроке.
  - Основной файл: [C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Data/MatchRepository.cs](C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Data/MatchRepository.cs)
  - Связанный UI/запрос: [C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Presentation/Views/StageMatchesPage.xaml.cs](C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Presentation/Views/StageMatchesPage.xaml.cs)
- **Тёмная тема:** заменить фиксированные цвета и проблемные прозрачные кнопки на theme-aware цвета через ресурсы/стили.
  - Глобальные ресурсы: [C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Resources/Styles/Colors.xaml](C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Resources/Styles/Colors.xaml), [C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Resources/Styles/Styles.xaml](C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Resources/Styles/Styles.xaml)
  - Экраны с наибольшими проблемами: [C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Presentation/Views/MatchEditPage.xaml](C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Presentation/Views/MatchEditPage.xaml), [C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Presentation/Views/StageMatchesPage.xaml](C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Presentation/Views/StageMatchesPage.xaml), [C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Presentation/Views/StageRosterPage.xaml](C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Presentation/Views/StageRosterPage.xaml), [C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Presentation/Views/TeamsListPage.xaml](C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/Presentation/Views/TeamsListPage.xaml), [C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/MainPage.xaml](C:/Users/antsi/.cursor/worktrees/HockeyTournamentTracker/kzb/MainPage.xaml)

## Технический подход

- Для фильтра:
  - Упростить SQL-ветку поиска (без `LOWER` по колонкам и без приведения строки запроса к инвариантному lower в C#).
  - Применять сравнение подстроки, устойчивое к кириллице/Unicode (через безопасную схему фильтрации без зависимости от ASCII-only `LOWER()` SQLite).
  - Проверить, что счётчик `Показано матчей` и список синхронны после ввода поиска.
- Для темы:
  - Вынести недостающие semantic-токены цветов в `Colors.xaml` (surface/card/border/secondary text/selected states).
  - В `Styles.xaml` добавить/обновить стили для `Frame`, `SearchBar`, «text/ghost» кнопок.
  - На экранах заменить фиксированные `#...` и `Gray` на `AppThemeBinding`/ресурсы.

## Проверка результата

- На экране матчей ввести часть названия команды (например, «Ак Барс») и убедиться, что матчи находятся.
- Проверить те же сценарии в светлой и тёмной теме:
  - список матчей,
  - редактирование матча,
  - стадии/участники,
  - состав стадии.
- Быстрая регрессия: фильтры периода/статуса, кнопки `Редактировать/Удалить`, читаемость текста и placeholder в поиске.

## Риски и контроль

- Риск: изменение стратегии фильтрации может затронуть пагинацию и производительность на больших объёмах.
  - Контроль: проверить корректность `totalCount`, страниц и времени отклика на типичном объёме данных.
- Риск: глобальные style-правки затронут экраны вне примеров.
  - Контроль: точечная проверка `MainPage`, `StageDetailsPage`, `TeamsListPage` после обновления ресурсов/стилей.

