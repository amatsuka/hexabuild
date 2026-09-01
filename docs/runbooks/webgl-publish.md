# Runbook: сборка WebGL и публикация в gh-pages

Живая процедура. Выполнять целиком, порядок значим.


Раньше это лежало разбросанно: настройки под Pages — в записи от 28.08, команда сборки — в записи
про веб-сборку для тестов, шаг публикации — одной строкой в `AGENTS.md`. Здесь весь порядок целиком.
Записи от 28.08 остаются как история того, почему пришли именно к этому.

1. **Проверить настройки.** `grep -nE "webGLCompressionFormat|webGLDecompressionFallback"
   ProjectSettings/ProjectSettings.asset` — для Pages нужно `compressionFormat: 1` (Gzip) и
   `decompressionFallback: 1`. Pages не отдаёт `Content-Encoding`, распаковку берёт загрузчик Unity.
   Если до этого гонялись локальные тесты с `2` (Disabled) — вернуть `1`, иначе поедет 53 МБ вместо 15.
2. **Собрать** через уже открытый редактор — вторым процессом нельзя, редактор держит лок на проекте:
   `unity command build --target WebGL --outputPath Build/Pages --confirm true`. Имя папки задаёт
   имена файлов (`Build/Pages.wasm.unityweb` и остальные), а `index.html` в ветке ссылается именно
   на них — папку не переименовывать.
3. **Проверить итог:** `unity command build_status --json`, смотреть поле `result` (нужно
   `Succeeded`), а не `status` — `completed` приходит и у провалившейся сборки. Плюс глазами:
   `ls -la Build/Pages/Build` — файлы должны быть свежие, всего около 16 МБ.
4. **Прогнать локально:** `python3 -m http.server 8000 --directory Build/Pages`, открыть
   `http://localhost:8000`. Файлом `index.html` не открывается, Unity WebGL требует HTTP.
5. **Опубликовать** — одним коммитом с `--force` из временного репозитория в `Build/Pages`, чтобы
   история сборок не копилась и `main` не раздувался:

   ```
   cd Build/Pages && rm -rf hexabuild_BurstDebugInformation_DoNotShip .git \
     && touch .nojekyll && git init -q && git add -A && git commit -qm "Deploy ..." \
     && git push --force git@github.com:amatsuka/hexabuild.git HEAD:gh-pages && rm -rf .git
   ```

   Отладочные символы Burst в вебе не нужны и в ветку не едут; пустой `.nojekyll` обязателен, иначе
   Jekyll перелопатит файлы на стороне Pages; `.git` убирается, чтобы не мешал следующей сборке.
6. **Синхронизировать локальный ref и посмотреть сайт:** `git fetch origin gh-pages` и
   `git branch -f gh-pages origin/gh-pages`, дальше https://amatsuka.github.io/hexabuild/ жёстким
   обновлением. `webGLDataCaching` включён, старая сборка может подтянуться из IndexedDB.

**Что настраивается один раз и не проверялось автоматикой:** Settings → Pages → Deploy from a branch
→ `gh-pages` / root. `gh` в системе не установлен, состояние настройки через API не смотрели —
`degraded`.

