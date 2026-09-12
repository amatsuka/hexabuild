# Аудио-бриф — Hex Colony

> Источник задания на генерацию звука и музыки. Не контракт на реализацию: снятие пункта
> «нет звука» из §1 `MVP_hex_colony.md` и стадия внедрения — отдельным решением человека.
>
> **Как пользоваться.** Каждая строка таблицы — одна заявка на генерацию: имя файла, что его
> запускает в игре, чем он должен быть, длина и промпт-подсказка на английском (сервисы
> генерации на русский реагируют хуже). Колонка «Триггер» — реальное событие в коде, точка
> подключения уже существует. Порядок работы — по разделу 8 «Очередь».

---

## 1. Звуковая эстетика

Ориентир один на все звуки, он же — общая часть промпта:

- **Мир:** казуальная стратегия-колония на гексах, вид сверху под наклоном, процедурный
  лоу-поли арт, тёплая палитра, стеклянные карточки интерфейса. Не реализм, не sci-fi,
  не мультяшный слэпстик.
- **Материалы:** дерево, камень, руда, земля, вода, ткань и бумага. Металл — тёплая бронза,
  не сталь.
- **Тон:** дружелюбный, чистый, короткий. Игрок кликает до 10 раз в секунду на поздних
  уровнях — звук обязан быть лёгким на сотом повторе, а не эффектным на первом.
- **Чего не надо:** баззеров, «ошибка!»-сигналов, голоса, ретро-8-бита, оркестровой эпики,
  громких хвостов и реверберации длиннее 0.5 с на игровых SFX.

Общий суффикс промпта для SFX:
`clean casual game sfx, low-poly cozy strategy, warm organic materials, short tail, dry, mono, no music, no voice`

Общий суффикс промпта для музыки:
`cozy casual strategy game music, warm acoustic, loopable, no vocals, unobtrusive, mid-tempo`

---

## 2. Технические требования

| Параметр | Значение |
|---|---|
| Формат сдачи | WAV 48 кГц / 16 бит |
| Каналы | SFX — моно; музыка и амбиенс — стерео |
| Нормализация | SFX −6 dBFS peak; музыка −14 LUFS integrated |
| Тишина | обрезать в ноль в начале, хвост оставить естественным |
| Лупы | бесшовные, без фейдов на стыке; равная длина у слоёв музыки |
| Импорт в Unity | SFX — Decompress On Load; музыка и амбиенс — Compressed In Memory, Vorbis q≈70 |
| Вес | лупы ≤ 2 МБ каждый — **Streaming в WebGL не работает**, всё лежит в памяти |
| Именование | ровно как в колонке «Файл», варианты суффиксом `_01`, `_02`, `_03` |

Дополнительно на стороне игры (не заявка на генерацию, а условия, под которые пишется звук):

- Микшер `Master` → `Music`, `Ambient`, `SFX`, `UI`. Дакинг `Music`/`Ambient` на паузе,
  на конце партии и на `sfx_storage_lost`.
- Приоритет перебивания тот же, что у визуала M29: потеря > премия за чистый склад > нагрев
  и обычные действия.
- Питч ±5–8% на всём, что звучит чаще раза в секунду. Пулы вариантов там, где помечено `×N`.
- Лимит голосов ~24 глобально, ~6 на добычу с кулдауном 60–80 мс на тип ресурса.
- Первый запуск звука — по первому клику игрока (`GameInput.Pressed` в меню): браузеры
  не дают автоплей.

---

## 3. Музыка

Три игровых слоя — **одна композиция, сведённая в три дорожки**, а не три трека: одинаковый
темп, тональность и длина, включаются кроссфейдом 1–2 с. Заказывать одним заданием со стемами.

| Файл | Где | Характер | Длина | Промпт-подсказка |
|---|---|---|---|---|
| `mus_menu_loop` | главное меню | тёплый, «карта на столе»: маримба, арфа, лёгкая фолк-перкуссия | 60–90 с | `cozy main menu loop, marimba and harp, gentle folk percussion, welcoming, calm` |
| `mus_game_base_loop` | партия, слой 1 | подкладной пульс: низкие струнные, пэд, мягкая перкуссия; не тянет внимание | 90–120 с | `ambient strategy underscore, low strings pad, soft hand percussion, non-intrusive loop` |
| `mus_game_mid_loop` | слой 2, по доле открытого поля | + мелодическая линия и бас | = слою 1 | `stem: melodic layer, plucked strings and warm bass, adds momentum` |
| `mus_game_heat_loop` | слой 3, выше 60% накала | + шейкер и хай-хэт: темп ощущается быстрее без смены BPM | = слою 1 | `stem: rhythmic layer, shaker and hi-hat, rising tension, same tempo` |
| `mus_contract_urgency_loop` | последние 10 с контракта | тихий пульсирующий слой поверх | 8–12 с | `tense pulsing loop, ticking urgency, quiet, sits under music` |
| `mus_gameover_win_stinger` | конец партии, поле пройдено | короткая фанфара, разрешение в мажор | 3–4 с | `short victory fanfare, warm brass and bells, uplifting resolve` |
| `mus_gameover_dead_stinger` | конец партии, тупик | нисходящий, спокойный — «партия закончилась», не траур | 2–3 с | `short descending outro sting, gentle, not tragic` |
| `mus_result_loop` | финальный экран | тихая реприза темы меню | 40–60 с | `quiet reprise of menu theme, sparse, reflective loop` |

---

## 4. Амбиенс

| Файл | Где | Характер | Длина | Промпт-подсказка |
|---|---|---|---|---|
| `amb_field_loop` | глобально в партии | ветер над полем, очень тихий | 30–60 с | `soft outdoor wind bed, distant, very quiet, seamless loop` |
| `amb_water_loop` | громкость по доле воды в кадре | плеск уреза, ручей | 30–60 с | `gentle stream and shoreline lapping, close but calm, seamless loop` |
| `amb_metropolis_loop` | позиционно у Метрополии | приглушённый гул поселения, редкие удары молота | 30–60 с | `distant medieval village hum, faint hammer taps, muffled, seamless loop` |

---

## 5. SFX поля

| Файл | Триггер | Характер | Длина | Промпт-подсказка |
|---|---|---|---|---|
| `sfx_tile_popup_open` | попап цены открытия | лёгкий «пуф» бумаги | 0.15 с | `paper popup whoosh, tiny, soft` |
| `sfx_tile_popup_dismiss` | клик мимо попапа | сухой шорох | 0.1 с | `paper dismiss, dry short swish` |
| `sfx_tile_open` | открытие плитки (`GameState.TileChanged`) | земляной подъём и отряхивание; главный «награда» звук поля | 0.5–0.7 с | `earth rising reveal, soil settling, satisfying warm thud` |
| `sfx_tile_open_wave` | волна открытия по соседям (`FieldPulse`) | низкий уходящий вуш, слоем под предыдущим | 0.8 с | `low outward whoosh, expanding ripple, soft` |
| `sfx_tile_open_forest` | вариант по содержимому | хвост поверх базового: листва | 0.3 с | `leaves rustle tail, short` |
| `sfx_tile_open_rock` | то же | камень | 0.3 с | `rock settle tail, short` |
| `sfx_tile_open_ore` | то же | металлический звон | 0.3 с | `metallic ore shimmer tail, short` |
| `sfx_tile_open_water` | то же | всплеск | 0.3 с | `small water splash tail, short` |
| `sfx_road_build` | постройка дороги | высыпанный щебень, утрамбовка | 0.35 с | `gravel poured and tamped, short construction sfx` |
| `sfx_bridge_build_wood` | мост на обычной плитке | доски, стук свай | 0.6 с | `wooden planks placed, pile driven, construction` |
| `sfx_bridge_build_stone` | мост на скале | каменная кладка | 0.6 с | `stone blocks set, masonry, construction` |
| `sfx_extract_wood` ×3–4 | `ProductionSystem.Produced`, дерево | короткий тюк топора | 0.12 с | `tiny axe chop on wood, dry, very short` |
| `sfx_extract_stone` ×3–4 | то же, камень | кирка по камню | 0.12 с | `tiny pickaxe on stone, dry, very short` |
| `sfx_extract_ore` ×3–4 | то же, руда | звонкий металлический скол | 0.12 с | `tiny metallic ore chip, bright, very short` |
| `sfx_deposit_depleted` | `ProductionSystem.TileDepleted` | осыпание, гаснущий хвост | 0.5 с | `crumbling depletion, fading out, small` |
| `sfx_delivery_arrive` | `DeliverySystem.Arrived`, прыжок в клетку | мягкий «плоп» приземления | 0.15 с | `soft pop landing, item drops into slot` |

Добыча — самый частый звук в игре: десятки срабатываний в секунду на поздних уровнях.
Клипы обязаны быть короткими, тихими и различимыми по типу ресурса, но не яркими.

---

## 6. SFX склада

Главный канал отдачи: склад — это рука игрока, и накал (M29) слышен раньше, чем виден.

| Файл | Триггер | Характер | Длина | Промпт-подсказка |
|---|---|---|---|---|
| `sfx_merge_small` | `MergeSystem.Merged`, 3→1 | сборка, короткий «шмяк» + звон результата | 0.3 с | `items snap together, small satisfying combine, light chime` |
| `sfx_merge_big` | `Merged`, 5→2 | плотнее и ниже, синхронно с `Hitstop` | 0.5 с | `heavy combine impact, deeper, punchy, brief chime` |
| `sfx_convert` ×2 | `MergeSystem.Converted`, обмен крафта на очки | короткий «ка-чинг» монеты | 0.2 с | `short coin cash register ding, bright, tiny` |
| `sfx_heat_step` | `ScoreMultiplier.Changed` вверх | одна нота питч-лестницы: 12 ступеней накала = 12 полутонов вверх | 0.1 с | `single short mallet note, clean, pitch-ladder step` |
| `sfx_heat_drain` | утечка накала после 2.5 с паузы | тихий уходящий шелест | 2 с луп | `quiet draining hiss, energy leaking away, loop` |
| `sfx_heat_burn` | `ScoreMultiplier.Burned`, переполнение сожгло накал | стеклянный треск/обрыв, слышен поверх всего | 0.6 с | `glass crack and power cut, sharp negative, cold` |
| `sfx_storage_lost` | `StorageGrid.ResourceLost` | резкий низкий удар + разбитое; самый «дорогой» негативный звук | 0.5 с | `resource destroyed, low impact with debris, harsh but clean` |
| `sfx_storage_sweep` | `MergeSystem.Swept`, премия за чистый склад | сияющий восходящий аккорд | 0.9 с | `sparkling ascending reward chord, bright, generous` |
| `sfx_refused` | `MergeSystem.Refused`, `GameState.ActionRefused` | сухой низкий «нельзя», без баззера | 0.15 с | `soft negative thud, muted denial, no buzzer` |

Питч-лестница `sfx_heat_step` и `sfx_convert` — то, ради чего затевается звук склада:
множитель должен быть слышен раньше, чем игрок посмотрит на число. `sfx_convert` звучит
до 10 раз в секунду — не длиннее 0.2 с и на 6–8 дБ тише мержа.

---

## 7. SFX контрактов, прогрессии и интерфейса

| Файл | Триггер | Характер | Длина | Промпт-подсказка |
|---|---|---|---|---|
| `sfx_contract_issued` | `ContractSystem.Issued` | разворачивающийся свиток + короткий рожок | 0.6 с | `parchment scroll unrolls, small horn call, quest offered` |
| `sfx_contract_progress` | `Progressed`, прилёт крафта в карточку | тик засчитанного, питч вверх по прогрессу | 0.15 с | `quest tick, small confirm blip, pitched step` |
| `sfx_contract_complete` | `Completed` | фанфара; питч вверх по ступени серии | 1.0 с | `quest complete fanfare, short warm brass and bells` |
| `sfx_contract_failed` | `Failed` | печать «просрочено», нисходящий | 0.6 с | `quest failed stamp, descending, dry, not harsh` |
| `sfx_contract_tick` | последние 10 с контракта | тиканье, ускоряется к нулю | 0.1 с | `clock tick, dry wooden, short` |
| `sfx_milestone` ×3 | `Milestones.Reached` (25/50/75%) | три ступени одного колокольчика, каждая выше | 0.7 с | `milestone bell, encouraging, ascending set of three` |
| `sfx_bar_gold` | бар пересёк 100% потолка | искристый переход в золото | 0.8 с | `golden sparkle surge, achievement shimmer` |
| `sfx_ui_press` | `GameInput.Pressed` / `PressPulse` | момент прижатия, тише отпускания | 0.08 с | `ui press down, soft tick` |
| `sfx_ui_click` | `UiButton` | подтверждённое нажатие | 0.1 с | `ui button click, clean, soft` |
| `sfx_ui_click_primary` | зелёная кнопка подтверждения, «Играть», «Следующий уровень» | плотнее и ниже обычного клика | 0.15 с | `primary confirm click, deeper, positive` |
| `sfx_ui_keypad` ×2 | цифры ввода сида | сухой тик клавиши | 0.08 с | `keypad tap, dry, tiny` |
| `sfx_ui_back` | «Назад», закрытие панели | обратный, ниже | 0.12 с | `ui back, downward soft click` |
| `sfx_pause_open` | `PauseView` открылась | вуш + приглушение мира | 0.4 с | `pause open whoosh, world muffles` |
| `sfx_pause_close` | `PauseView` закрылась | обратный | 0.4 с | `pause close whoosh, world returns` |
| `sfx_level_start` | выбор карты, переход в партию | вуш + удар | 0.8 с | `level start whoosh with impact, energetic, warm` |
| `sfx_screen_dim` | затемнение перед финальным экраном | низкий уход | 0.6 с | `low fade to black swell, soft` |
| `sfx_result_count` | набегание чисел на финальном экране | тик на шаг | 0.06 с | `score counter tick, tiny, rapid-safe` |
| `sfx_star_1` / `_2` / `_3` | звёзды уровня, по одной | питч вверх ступенями | 0.5 с | `star awarded chime, ascending three-step set` |
| `sfx_record` | плашка нового рекорда | яркий короткий акцент | 0.6 с | `new record accent, bright, proud` |
| `sfx_crown` | корона за пройденное поле | торжественный акцент | 0.8 с | `crown awarded, regal short flourish` |
| `sfx_confetti` | залп конфетти, синхронно с короной | бумажный хлопок и шелест | 0.7 с | `confetti pop and paper flutter, celebratory` |

---

## 8. Очередь и объём

Итого около **60 файлов**, из них 8 музыкальных и 3 амбиентных лупа.

**P0 — минимум, после которого игра звучит (~20 файлов).**
`sfx_ui_click`, `sfx_tile_open`, `sfx_road_build`, `sfx_extract_wood/stone/ore`,
`sfx_delivery_arrive`, `sfx_merge_small`, `sfx_merge_big`, `sfx_convert`, `sfx_heat_step`,
`sfx_storage_lost`, `sfx_refused`, `sfx_contract_issued/complete/failed`,
`mus_game_base_loop`, `mus_menu_loop`.

**P1 — глубина (~25 файлов).**
Накал целиком (`heat_drain`, `heat_burn`, `storage_sweep`), вехи и бар, звёзды и финальный
экран, мосты, биомные хвосты открытия, амбиенс, стингеры конца партии.

**P2 — полировка (~15 файлов).**
Слои `mus_game_mid/heat_loop`, `mus_contract_urgency_loop`, корона и конфетти, вариации UI
и клавиатуры, `sfx_result_count`, `sfx_screen_dim`.

---

## 9. Приёмка

Файл принят, если:

1. Имя, длина, каналы и нормализация — по таблице и разделу 2.
2. Луп сшивается без щелчка и без слышимого «шва» на втором круге.
3. SFX не теряется в миксе поверх `mus_game_base_loop` на штатной громкости.
4. Клип, который звучит чаще раза в секунду, выдерживает 30 повторов подряд с питчем ±8%
   и не начинает раздражать.
5. У пулов `×N` варианты различимы, но читаются одним звуком, а не тремя разными.
