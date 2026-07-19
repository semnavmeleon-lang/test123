using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Teammate Verdict", "semnavmeleon", "1.0.0")]
    [Description("Real Rust teammate drama, one random scenario at a time, two blunt verdicts to pick from.")]
    public class TeammateVerdict : RustPlugin
    {
        private PluginConfig _config;

        private readonly Dictionary<ulong, int> _lastIndex = new Dictionary<ulong, int>();

        private class Scenario
        {
            [JsonProperty("Situation")]
            public string Situation;

            [JsonProperty("Option A")]
            public string OptionA;

            [JsonProperty("Option B")]
            public string OptionB;
        }

        private class PluginConfig
        {
            [JsonProperty("Scenarios")]
            public List<Scenario> Scenarios = new List<Scenario>
            {
                new Scenario { Situation = "Тиммейт потратил взрывчатку, пока ты был в оффлайне.", OptionA = "Простить", OptionB = "Взорвать его шкаф" },
                new Scenario { Situation = "Тиммейт пересмотрел \"Держи дверь\" и теперь вся ткань клана уходит на обои.", OptionA = "Разрешить", OptionB = "Сжечь обои" },
                new Scenario { Situation = "Тиммейт построил магазин, к которому не может подлететь дрон доставки.", OptionA = "Похвалить", OptionB = "Снести" },
                new Scenario { Situation = "Тиммейт нашёл сгнивший (упавший по шкафу) дом, полный лута.", OptionA = "Поделить", OptionB = "Скрыть находку" },
                new Scenario { Situation = "Тиммейт нашёл эко-рейд, с которого вынесли больше, чем с обычного.", OptionA = "Признать гением", OptionB = "Обвинить в читах" },
                new Scenario { Situation = "Тиммейт опоздал на вайп на несколько часов, стоит в очереди из 200 человек.", OptionA = "Ждать его", OptionB = "Начать без него" },
                new Scenario { Situation = "Тиммейт залагал посреди файта, и его обоссали на глазах у всей команды.", OptionA = "Отомстить", OptionB = "Заскринить и поржать" },
                new Scenario { Situation = "Тиммейт зафармил редкий тыквенный шлем на ивенте и не поделился.", OptionA = "Простить", OptionB = "Отобрать силой" },
                new Scenario { Situation = "Тиммейт спалил код от главной двери в общий чат по привычке.", OptionA = "Сменить код", OptionB = "Выгнать из клана" },
                new Scenario { Situation = "Тиммейт притащил ручного медведя с монумента, тот сожрал половину курятника.", OptionA = "Оставить питомцем", OptionB = "Пристрелить" },
                new Scenario { Situation = "Тиммейт зафрендил в игре рейдера, который вчера снёс вашу вышку.", OptionA = "Довериться", OptionB = "Забанить" },
                new Scenario { Situation = "Тиммейт скачал чит \"на всякий случай\", клянётся, что не включал.", OptionA = "Поверить", OptionB = "Кикнуть немедленно" },
                new Scenario { Situation = "Тиммейт единолично продержал весь лут с Патрулирующего вертолёта, хотя сбивала вся команда.", OptionA = "Простить", OptionB = "Забрать всё" },
                new Scenario { Situation = "Тиммейт назвал клан именем, из-за которого вас теперь троллят в чате.", OptionA = "Оставить, это мем", OptionB = "Сменить имя" },
                new Scenario { Situation = "Тиммейт улетел на минивертолёте за ресурсами и не вернулся, слив весь бензин впустую.", OptionA = "Ждать", OptionB = "Списать со счетов" },
                new Scenario { Situation = "Тиммейт врубил в войсе музыку на весь клан без предупреждения.", OptionA = "Терпеть", OptionB = "Кикнуть из войса" },
                new Scenario { Situation = "Тиммейт построил лифт без выхода наверх, вы застряли на 3 этаже вдвоём.", OptionA = "Смеяться вместе", OptionB = "Свалить вину" },
                new Scenario { Situation = "Тиммейт продал общий скрап на скины лично себе через Steam Marketplace.", OptionA = "Простить", OptionB = "Забрать вещи" },
                new Scenario { Situation = "Тиммейт зазвал в клан рандома, который оказался шпионом вражеского клана.", OptionA = "Простить", OptionB = "Сжечь базу на всякий случай" },
                new Scenario { Situation = "Тиммейт заминировал общий проход и подорвался на своей же мине.", OptionA = "Обнять", OptionB = "Заставить чинить самому" },
                new Scenario { Situation = "Тиммейт всю ночь фармил дерево топором вместо того, чтобы идти в рейд по плану.", OptionA = "Похвалить", OptionB = "Высмеять" },
                new Scenario { Situation = "Тиммейт вызвал Bradley на себя и погиб, забрав с собой половину командного лута во взрыве.", OptionA = "Похоронить с почестями", OptionB = "Обозвать криворуким" },
                new Scenario { Situation = "Тиммейт разбудил спящего медведя рядом с базой.", OptionA = "Помочь отбиться", OptionB = "Убежать первым" },
                new Scenario { Situation = "Тиммейт закинул в ТС последний ресурс, а сам остался без брони.", OptionA = "Прикрыть", OptionB = "Бросить" },
                new Scenario { Situation = "Тиммейт взял себе весь скрап с NPC на монументе.", OptionA = "Простить", OptionB = "Отобрать" },
                new Scenario { Situation = "Тиммейт спалил вейпоинт базы в общем чате сервера.", OptionA = "Переехать всей базой", OptionB = "Выгнать" },
                new Scenario { Situation = "Тиммейт сказал зашедшему в гости, что ловушек нет — а они есть.", OptionA = "Похвалить наглость", OptionB = "Извиниться перед гостем" },
                new Scenario { Situation = "Тиммейт потерял последний ключ от сейфа.", OptionA = "Взломать вместе", OptionB = "Обвинить" },
                new Scenario { Situation = "Тиммейт зафармил всю серу под носом у клана-соседа.", OptionA = "Гордиться", OptionB = "Извиниться перед соседями" },
                new Scenario { Situation = "Тиммейт продал ядерную ракету на аукцион до конца ивента.", OptionA = "Простить", OptionB = "Отобрать долю" },
                new Scenario { Situation = "Тиммейт спрятал заначку с оружием отдельно от общего склада.", OptionA = "Понять", OptionB = "Конфисковать" },
                new Scenario { Situation = "Тиммейт зашёл в чужой клан \"на разведку\" и застрял там навсегда.", OptionA = "Считать предателем", OptionB = "Тоже перейти" },
                new Scenario { Situation = "Тиммейт спалил все патроны по курице.", OptionA = "Посмеяться", OptionB = "Заставить фармить свинец" },
                new Scenario { Situation = "Тиммейт устроил вечеринку на сервере в вечер важного рейда.", OptionA = "Присоединиться", OptionB = "Рейдить без него" },
                new Scenario { Situation = "Тиммейт зарейдил монумент без брони и умер с общим лутом.", OptionA = "Простить", OptionB = "Помянуть с укором" },
                new Scenario { Situation = "Тиммейт купил кастом-скин на всю зарплату вместо доната клану.", OptionA = "Одобрить", OptionB = "Устыдить" },
                new Scenario { Situation = "Тиммейт спалил секретный вход в базу в своём стриме.", OptionA = "Заделать срочно", OptionB = "Кикнуть" },
                new Scenario { Situation = "Тиммейт зафрендил бота из Steam, который спамит рекламой в клановый чат.", OptionA = "Игнорировать", OptionB = "Забанить бота" },
                new Scenario { Situation = "Тиммейт назвал клан матерным словом при онлайн-модераторе.", OptionA = "Оставить", OptionB = "Срочно переименовать" },
                new Scenario { Situation = "Тиммейт скинул лут врагу вместо союзника по ошибке.", OptionA = "Простить", OptionB = "Считать предательством" },
                new Scenario { Situation = "Тиммейт застрял в текстуре после апдейта и потерял всё.", OptionA = "Помочь с саппортом", OptionB = "Списать как невезение" },
                new Scenario { Situation = "Тиммейт впервые зашёл на сервер и сразу поставил ТС в чужом периметре.", OptionA = "Объяснить по-доброму", OptionB = "Снести без разговоров" },
                new Scenario { Situation = "Тиммейт разнёс свою же ловушку по невнимательности.", OptionA = "Посмеяться", OptionB = "Вычесть из доли" },
                new Scenario { Situation = "Тиммейт стримил координаты базы, не скрывая оверлей.", OptionA = "Довериться зрителям", OptionB = "Эвакуироваться" },
                new Scenario { Situation = "Тиммейт отказался чинить ТС, потому что \"не его смена\".", OptionA = "Понять усталость", OptionB = "Выгнать" },
                new Scenario { Situation = "Тиммейт взял альфа-кит на другого персонажа и не поделился рецептами.", OptionA = "Простить", OptionB = "Забрать доступ" },
                new Scenario { Situation = "Тиммейт нашёл секретную комнату у рейдеров и всё разболтал в общем чате.", OptionA = "Похвалить находку", OptionB = "Обвинить в сливе" },
                new Scenario { Situation = "Тиммейт запаниковал на миникоптере и разбил его о скалу.", OptionA = "Починить вместе", OptionB = "Заставить чинить одного" },
                new Scenario { Situation = "Тиммейт продал вам же украденный из клана лут со скидкой.", OptionA = "Посмеяться", OptionB = "Устроить разборки" },
                new Scenario { Situation = "Тиммейт устроил засаду на союзника ради шутки, тот обиделся всерьёз.", OptionA = "Извиниться за него", OptionB = "Сказать, что тот слишком серьёзный" },
                new Scenario { Situation = "Тиммейт спрятался в шкафу от рейдеров и не отвечал на СОС.", OptionA = "Понять страх", OptionB = "Считать трусом" },
                new Scenario { Situation = "Тиммейт скинул поддельный рецепт крафта, найденный в интернете.", OptionA = "Посмеяться", OptionB = "Обидеться всерьёз" },
                new Scenario { Situation = "Тиммейт взял с собой всю аптечку в соло-вылазку.", OptionA = "Понять", OptionB = "Обвинить в жадности" },
                new Scenario { Situation = "Тиммейт устроил дуэль на кулаках с админом сервера ради мема.", OptionA = "Гордиться", OptionB = "Отговорить от бана" },
                new Scenario { Situation = "Тиммейт забыл продлить сервер вовремя, и вайп пропал.", OptionA = "Простить", OptionB = "Требовать компенсацию" },
                new Scenario { Situation = "Тиммейт спалил всю нефть на генератор ради света в подвале.", OptionA = "Понять уют", OptionB = "Пожурить" },
                new Scenario { Situation = "Тиммейт устроил гонки на лошадях вместо фарма перед рейдом.", OptionA = "Присоединиться", OptionB = "Напомнить о приоритетах" },
                new Scenario { Situation = "Тиммейт назвал себя лидером клана без голосования.", OptionA = "Смириться", OptionB = "Устроить перевыборы" },
                new Scenario { Situation = "Тиммейт спустил все сатоши коины в казино-плагине сервера.", OptionA = "Посмеяться", OptionB = "Предупредить об азарте" },
                new Scenario { Situation = "Тиммейт спрятал заначку голды на аукционе втайне от всех.", OptionA = "Понять", OptionB = "Обвинить в жадности" },
                new Scenario { Situation = "Тиммейт устроил пранк с фальшивой тревогой о рейде в 3 ночи.", OptionA = "Отомстить тем же", OptionB = "Простить как шутку" },
                new Scenario { Situation = "Тиммейт слил инфу о ресурсах клана нейтралам за услугу.", OptionA = "Понять расчёт", OptionB = "Считать предательством" },
                new Scenario { Situation = "Тиммейт взял добычу с последнего Cargo Ship себе целиком.", OptionA = "Простить, он рисковал", OptionB = "Забрать половину" },
                new Scenario { Situation = "Тиммейт отказался помогать в файте, потому что \"качал скиллы соло\".", OptionA = "Понять", OptionB = "Кикнуть" },
                new Scenario { Situation = "Тиммейт устроил конкурс прыжков с катапульты, команда потеряла половину состава.", OptionA = "Посмеяться", OptionB = "Запретить катапульты" },
                new Scenario { Situation = "Тиммейт повесил вражеский баннер на вашу вышку ради шутки.", OptionA = "Оставить как арт", OptionB = "Снести" },
                new Scenario { Situation = "Тиммейт назвал общую находку своим личным везением при всех.", OptionA = "Простить бахвальство", OptionB = "Напомнить, кто нашёл первым" },
                new Scenario { Situation = "Тиммейт разболтал пароль от клан-чата постороннему другу.", OptionA = "Сменить тихо", OptionB = "Устроить разбор" },
                new Scenario { Situation = "Тиммейт купил привилегию на сервере из общего бюджета клана без спроса.", OptionA = "Одобрить постфактум", OptionB = "Требовать вернуть" },
                new Scenario { Situation = "Тиммейт зафармил лут с ивент-босса и продал скин на сторону.", OptionA = "Порадоваться за него", OptionB = "Считать общим" },
                new Scenario { Situation = "Тиммейт устроил истерику в чате из-за проигранного файта.", OptionA = "Успокоить", OptionB = "Замьютить" },
                new Scenario { Situation = "Тиммейт слил стратегию клана сопернику по дружбе перед рейдом.", OptionA = "Считать глупостью", OptionB = "Считать предательством" },
                new Scenario { Situation = "Тиммейт взял на себя роль казначея клана и не отчитывается о тратах.", OptionA = "Доверять дальше", OptionB = "Требовать отчёт" },
                new Scenario { Situation = "Тиммейт разбудил весь сервер ночным фейерверком у себя на базе.", OptionA = "Присоединиться", OptionB = "Устроить разборки" },
                new Scenario { Situation = "Тиммейт назвал общую победу командной работой, хотя всё сделал сам он.", OptionA = "Согласиться из вежливости", OptionB = "Уточнить факты" },
                new Scenario { Situation = "Тиммейт устроил русскую рулетку с патронами дробовика на спор.", OptionA = "Отговорить", OptionB = "Наблюдать с интересом" },
                new Scenario { Situation = "Тиммейт скрыл, что видел вражеский клан у ваших границ.", OptionA = "Простить забывчивость", OptionB = "Считать саботажем" },
                new Scenario { Situation = "Тиммейт взял в долг скрап у соседнего клана без вашего ведома.", OptionA = "Признать долг общим", OptionB = "Заставить вернуть лично" },
                new Scenario { Situation = "Тиммейт устроил стрим-снайпинг на противника чужим донат-статусом.", OptionA = "Считать находчивостью", OptionB = "Считать читерством" },
                new Scenario { Situation = "Тиммейт забрал себе весь клановый донат-бонус за месяц.", OptionA = "Простить", OptionB = "Требовать раздела" },
                new Scenario { Situation = "Тиммейт затеял спор с рейдерами прямо во время осады вместо обороны.", OptionA = "Оценить дерзость", OptionB = "Отругать" },
                new Scenario { Situation = "Тиммейт спалил последнюю ракету по кусту вместо стены врага.", OptionA = "Посмеяться", OptionB = "Вычесть из доли" },
                new Scenario { Situation = "Тиммейт назвал вашу базу \"временной помойкой\" при госте.", OptionA = "Пропустить мимо ушей", OptionB = "Обидеться" },
                new Scenario { Situation = "Тиммейт без спроса взял последний костюм НВГ перед ночным рейдом.", OptionA = "Уступить", OptionB = "Отобрать назад" },
                new Scenario { Situation = "Тиммейт устроил фейерверк на крыше ТС прямо во время рейда врагов.", OptionA = "Признать смелость", OptionB = "Отругать за безумие" },
                new Scenario { Situation = "Тиммейт вложил все сбережения клана в сатоши коины на пике курса.", OptionA = "Довериться курсу", OptionB = "Изъять монеты в фонд" },
                new Scenario { Situation = "Тиммейт выложил скриншот вашей базы в паблик-чат сервера ради лайков.", OptionA = "Простить тщеславие", OptionB = "Срочно переехать" },
                new Scenario { Situation = "Тиммейт единолично объявил войну соседнему клану от имени всех.", OptionA = "Поддержать", OptionB = "Отменить и извиниться" },
                new Scenario { Situation = "Тиммейт спрятал последний банан от голодной команды.", OptionA = "Понять голод", OptionB = "Отобрать и поделить" },
                new Scenario { Situation = "Тиммейт вызвал босса монумента на дуэль без брони, \"чтобы было честно\".", OptionA = "Уважать принцип", OptionB = "Заставить одеться" },
                new Scenario { Situation = "Тиммейт назвал клан \"временным\" при новичках, все разбежались.", OptionA = "Пошутить в ответ", OptionB = "Срочный ребрендинг" },
                new Scenario { Situation = "Тиммейт взял в личное пользование весь фермерский участок клана.", OptionA = "Уважать труд", OptionB = "Поделить землю" },
                new Scenario { Situation = "Тиммейт спалил последний хэллоуин-костюм на дуэли с пугалом.", OptionA = "Посмеяться", OptionB = "Заставить компенсировать" },
                new Scenario { Situation = "Тиммейт устроил тотализатор на исход рейда среди зрителей стрима.", OptionA = "Одобрить движ", OptionB = "Запретить ставки" },
                new Scenario { Situation = "Тиммейт назвал общую стратегию своей интуицией, хотя план был не его.", OptionA = "Не спорить", OptionB = "Настоять на признании" },
                new Scenario { Situation = "Тиммейт взял в разведку весь боезапас клана \"на всякий случай\".", OptionA = "Довериться расчёту", OptionB = "Забрать половину" },
                new Scenario { Situation = "Тиммейт нагрубил новичку, который просто просил помощи у костра.", OptionA = "Извиниться за него", OptionB = "Поддержать грубость" },
                new Scenario { Situation = "Тиммейт вложил весь клановый скрап в лотерею сервера ради джекпота.", OptionA = "Простить азарт", OptionB = "Запретить ставки" },
                new Scenario { Situation = "Тиммейт скрыл, что первым нашёл координаты секретного монумента.", OptionA = "Понять желание сюрприза", OptionB = "Обвинить в утаивании" },
                new Scenario { Situation = "Тиммейт объявил себя \"императором вайпа\" после одной удачной серии рейдов.", OptionA = "Признать титул шутки ради", OptionB = "Устроить переворот" },
            };
        }

        #region Config

        protected override void LoadDefaultConfig() => _config = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null) throw new Exception("null config");
            }
            catch
            {
                PrintWarning("Config is corrupt, loading defaults.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        private void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoScenarios"] = "Сценарии не загружены.",
                ["Prompt"] = "{0}\nA) {1}\nB) {2}\n/verdict a  или  /verdict b",
                ["NeedPrompt"] = "Сначала /verdict — получить ситуацию.",
                ["Chosen"] = "Ты выбрал: {0}",
                ["Usage"] = "/verdict — новая ситуация | /verdict a|b — ответить",
            }, this);
        }

        [ChatCommand("verdict")]
        private void CmdVerdict(BasePlayer player, string command, string[] args)
        {
            if (_config.Scenarios == null || _config.Scenarios.Count == 0)
            {
                SendReply(player, Lang("NoScenarios", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                var index = UnityEngine.Random.Range(0, _config.Scenarios.Count);
                _lastIndex[player.userID] = index;
                var scenario = _config.Scenarios[index];
                SendReply(player, Lang("Prompt", player.UserIDString, scenario.Situation, scenario.OptionA, scenario.OptionB));
                return;
            }

            var choice = args[0].ToLower();
            if (choice != "a" && choice != "b")
            {
                SendReply(player, Lang("Usage", player.UserIDString));
                return;
            }

            if (!_lastIndex.TryGetValue(player.userID, out var lastIndex))
            {
                SendReply(player, Lang("NeedPrompt", player.UserIDString));
                return;
            }

            var picked = _config.Scenarios[lastIndex];
            var pickedText = choice == "a" ? picked.OptionA : picked.OptionB;
            SendReply(player, Lang("Chosen", player.UserIDString, pickedText));
        }

        private string Lang(string key, string userId, params object[] args) =>
            string.Format(lang.GetMessage(key, this, userId), args);
    }
}
