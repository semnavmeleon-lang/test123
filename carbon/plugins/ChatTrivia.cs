using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Chat Trivia", "semnavmeleon", "1.1.0")]
    [Description("Broadcasts a random question to global chat every N minutes; every question has a checkable answer.")]
    public class ChatTrivia : RustPlugin
    {
        private const string PermAdmin = "chattrivia.admin";

        [PluginReference] private Plugin SatoshiCoins;

        private PluginConfig _config;
        private int _lastIndex = -1;
        private Round _round;

        private class Question
        {
            [JsonProperty("Category")]
            public string Category;

            // Лёгкий | Средний | Сложный -- controls the reward multiplier for this question's round.
            [JsonProperty("Difficulty")]
            public string Difficulty = "Средний";

            [JsonProperty("Variants")]
            public List<string> Variants;

            // Acceptable answer keywords (case-insensitive substring match).
            [JsonProperty("Answer")]
            public List<string> Answer;
        }

        private class Round
        {
            public List<string> Answer;
            public string Difficulty;
            public Dictionary<ulong, int> Attempts = new Dictionary<ulong, int>();
            public List<ulong> Winners = new List<ulong>();
        }

        private class PluginConfig
        {
            [JsonProperty("Interval (seconds)")]
            public float IntervalSeconds = 1800f;

            [JsonProperty("Chat prefix")]
            public string Prefix = "[Вопрос дня]";

            [JsonProperty("Answer window (seconds)")]
            public float AnswerWindowSeconds = 30f;

            [JsonProperty("Max attempts per player")]
            public int MaxAttempts = 2;

            [JsonProperty("Reward per winner (satoshi, before BTC scaling)")]
            public int RewardPerWinner = 5;

            [JsonProperty("Reward multiplier: Лёгкий")]
            public double EasyMultiplier = 1.0;

            [JsonProperty("Reward multiplier: Средний")]
            public double MediumMultiplier = 1.5;

            [JsonProperty("Reward multiplier: Сложный")]
            public double HardMultiplier = 2.5;

            [JsonProperty("Nonsense replacements for a correct answer's chat message")]
            public List<string> NonsenseReplies = new List<string>
            {
                "бип-боп, я робот",
                "42",
                "а мужики-то не знают",
                "сегодня хорошая погода, не находите?",
                "это должно было остаться в чате",
                "ку-ку",
                "нет, это не подсказка",
                "чебурек с сыром",
                "занято",
                "系统繁忙",
            };

            [JsonProperty("Questions")]
            public List<Question> Questions = new List<Question>
            {
                // Философия -- переформулирована как викторина об авторстве/терминах,
                // у открытых метафизических вопросов в принципе нет проверяемого ответа.
new Question { Category = "Философия", Answer = new List<string> { "детерминизм", "determinism" }, Variants = new List<string>
                {
                    "Как называется философская позиция, согласно которой все наши решения предопределены предшествующими причинами, а свободы воли не существует?",
                    "Философский вопрос: назови термин для мировоззрения, где каждое решение было предопределено ещё до нашего рождения.",
                    "Викторина: как называется учение, отрицающее свободу воли в пользу полной предопределённости событий?",
                }},
                
new Question { Category = "Философия", Answer = new List<string> { "беркли", "berkeley" }, Variants = new List<string>
                {
                    "Какой философ считал, что вещи существуют только пока их кто-то воспринимает — отсюда и вопрос про дерево, падающее без свидетелей?",
                    "Философский вопрос: назови автора идеи \"существовать значит быть воспринимаемым\".",
                    "Викторина: чьё имя стоит за классическим \"звук падающего дерева в лесу\" — назови философа.",
                }},
                
new Question { Category = "Философия", Answer = new List<string> { "кант", "kant" }, Variants = new List<string>
                {
                    "Какой немецкий философ считал, что поступок нравственен только тогда, когда совершён из чувства долга, а не из страха или выгоды?",
                    "Философский вопрос: назови автора учения о моральном долге, отрицающего ценность поступков из страха наказания.",
                    "Викторина: какой мыслитель разделил поступки \"из долга\" и поступки \"по расчёту\" или из страха?",
                }},
                
new Question { Category = "Философия", Answer = new List<string> { "ницше", "nietzsche" }, Variants = new List<string>
                {
                    "Какой философ ввёл понятие \"вечное возвращение\" — идею о готовности прожить свою жизнь заново бесконечное число раз?",
                    "Философский вопрос: назови автора концепции вечного возвращения одной и той же жизни.",
                    "Викторина: чья идея — что нужно жить так, будто проживёшь эту же жизнь ещё бесчисленное количество раз?",
                }},
                
new Question { Category = "Философия", Answer = new List<string> { "идеализм", "солипсизм" }, Variants = new List<string>
                {
                    "Как называется философская позиция, отрицающая существование объективной реальности вне нашего восприятия?",
                    "Философский вопрос: назови термин для учения, где реальность существует только как чья-то интерпретация.",
                    "Викторина: как называется взгляд, что мир таков, каким мы его воспринимаем, и не более того?",
                }},
                
new Question { Category = "Философия", Answer = new List<string> { "консеквенциализм", "утилитаризм" }, Variants = new List<string>
                {
                    "Как называется этическое направление, оценивающее поступок только по его последствиям, а не по намерениям?",
                    "Философский вопрос: назови этическую теорию, где важен только результат поступка, а не мотив.",
                    "Викторина: какое направление в этике судит поступок исключительно по его последствиям?",
                }},
                
new Question { Category = "Философия", Answer = new List<string> { "проблема других сознаний", "чужого сознания", "other minds" }, Variants = new List<string>
                {
                    "Как называется философская проблема, ставящая под сомнение, можем ли мы вообще узнать, что у других людей есть сознание, похожее на наше?",
                    "Философский вопрос: назови проблему о том, что мы никогда не можем напрямую проверить чужое сознание.",
                    "Викторина: как называется классическая проблема о недоказуемости чужого внутреннего опыта?",
                }},
                
new Question { Category = "Философия", Answer = new List<string> { "машина переживаний", "experience machine", "нозик", "nozick" }, Variants = new List<string>
                {
                    "Как называется мысленный эксперимент про машину, которая может дать тебе иллюзию идеального счастья — согласился бы ты подключиться?",
                    "Философский вопрос: назови мысленный эксперимент про подключение к машине с идеальным счастьем.",
                    "Викторина: как называется эксперимент, спрашивающий, согласился бы ты жить в симуляции ради чистого счастья?",
                }},
                
new Question { Category = "Философия", Answer = new List<string> { "макиавелли", "machiavelli" }, Variants = new List<string>
                {
                    "Какому итальянскому мыслителю эпохи Возрождения приписывают принцип \"цель оправдывает средства\"?",
                    "Философский вопрос: назови автора \"Государя\", с чьим именем связывают фразу про цель и средства.",
                    "Викторина: чьё имя стало нарицательным для беспринципной, но эффективной политики ради цели?",
                }},
                
new Question { Category = "Философия", Answer = new List<string> { "экзистенциализм", "existentialism" }, Variants = new List<string>
                {
                    "Какое философское течение утверждает, что жизнь не имеет заранее заданного смысла, и его нужно создавать самому?",
                    "Философский вопрос: назови течение, где смысл жизни не дан изначально, а создаётся самим человеком.",
                    "Викторина: как называется направление философии, из которого вышел миф о Сизифе Камю?",
                }},

                // Китайская чайная культура
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "декарт", "descartes" }, Variants = new List<string> { "Кто автор фразы \"Я мыслю, следовательно существую\"?", "Викторина: назови философа, сказавшего \"мыслю, значит существую\"." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "сократ", "socrates" }, Variants = new List<string> { "Какой древнегреческий философ был приговорён к смерти и выпил цикуту?", "Вопрос: назови философа, казнённого цикутой в Афинах." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "платон", "plato" }, Variants = new List<string> { "Кто написал \"Государство\" с описанием идеального устройства общества?", "Викторина: автор диалога \"Государство\" об идеальном полисе." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "аристотель", "aristotle" }, Variants = new List<string> { "Какой философ был учеником Платона и учителем Александра Македонского?", "Вопрос: назови учителя Александра Македонского среди философов." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "ницше", "nietzsche" }, Variants = new List<string> { "Кто автор труда \"Так говорил Заратустра\"?", "Викторина: назови автора \"Так говорил Заратустра\"." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "фрейд", "freud" }, Variants = new List<string> { "Кто разработал психоанализ как метод и теорию личности?", "Вопрос: назови основателя психоанализа." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "кант", "kant" }, Variants = new List<string> { "Кто ввёл понятие \"категорический императив\"?", "Викторина: назови автора категорического императива." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "камю", "camus" }, Variants = new List<string> { "Какой французский философ, лауреат Нобелевской премии по литературе, связан с абсурдизмом?", "Вопрос: назови автора-абсурдиста, лауреата Нобелевки." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "гоббс", "hobbes" }, Variants = new List<string> { "Кто автор \"Левиафана\" — трактата о государстве как общественном договоре?", "Викторина: назови автора \"Левиафана\"." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "диоген", "diogenes" }, Variants = new List<string> { "Какой философ жил в бочке и считается основателем кинизма?", "Вопрос: назови философа из бочки." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "маркс", "marx" }, Variants = new List<string> { "Кто написал \"Капитал\" и разработал теорию классовой борьбы?", "Викторина: назови автора \"Капитала\"." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "локк", "locke" }, Variants = new List<string> { "Какой английский философ считается отцом эмпиризма и писал про сознание как \"чистый лист\"?", "Вопрос: назови автора идеи tabula rasa." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "руссо", "rousseau" }, Variants = new List<string> { "Кто автор трактата \"Об общественном договоре\"?", "Викторина: назови автора \"Общественного договора\"." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "рэнд", "rand" }, Variants = new List<string> { "Какая писательница-философ основала объективизм и написала \"Атлант расправил плечи\"?", "Вопрос: назови автора объективизма и \"Атланта\"." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "сартр", "sartre" }, Variants = new List<string> { "Кто из философов сказал \"Ад — это другие люди\"?", "Викторина: назови автора фразы про ад из других людей." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "гегель", "hegel" }, Variants = new List<string> { "Кто разработал диалектику \"тезис — антитезис — синтез\"?", "Вопрос: назови автора диалектической триады." }},
                
new Question { Category = "Философия", Difficulty = "Лёгкий", Answer = new List<string> { "хайдеггер", "heidegger" }, Variants = new List<string> { "Кто автор \"Бытия и времени\", ключевого текста экзистенциализма?", "Викторина: назови автора \"Бытия и времени\"." }},

                // Философия -- средний
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "зенон китийский", "zeno of citium" }, Variants = new List<string> { "Какой античный философ считается основателем стоицизма?", "Вопрос: назови основателя школы стоицизма." }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "юнг", "jung" }, Variants = new List<string> { "Кто ввёл понятия \"архетип\" и \"коллективное бессознательное\"?", "Викторина: назови автора теории архетипов." }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "оккам", "ockham", "occam" }, Variants = new List<string> { "Какой философ сформулировал принцип \"не следует множить сущности без необходимости\"?", "Вопрос: чьё имя носит знаменитая \"бритва\" в философии?" }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "шрёдингер", "schrodinger", "шредингер" }, Variants = new List<string> { "Чьё имя носит мысленный эксперимент с котом, который одновременно жив и мёртв?", "Викторина: назови автора мысленного эксперимента про кота." }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "гоббс", "hobbes" }, Variants = new List<string> { "Какой философ считал, что \"человек человеку волк\"?", "Вопрос: назови автора фразы \"человек человеку волк\"." }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "платон", "plato" }, Variants = new List<string> { "Кто автор притчи о пещере, где узники видят только тени на стене?", "Викторина: назови автора аллегории пещеры." }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "гуссерль", "husserl" }, Variants = new List<string> { "Какой немецкий философ разработал феноменологию как философский метод?", "Вопрос: назови основателя феноменологии." }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "ницше", "nietzsche" }, Variants = new List<string> { "Кто ввёл понятие \"воля к власти\" как движущую силу человека?", "Викторина: назови автора понятия \"воля к власти\"." }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "монтескьё", "montesquieu" }, Variants = new List<string> { "Какой философ Просвещения обосновал разделение властей на три ветви?", "Вопрос: назови автора теории разделения властей." }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "кант", "kant" }, Variants = new List<string> { "Кто автор понятия категорического императива в его практической форме?", "Викторина: чьё имя связано с практическим применением категорического императива?" }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "витгенштейн", "wittgenstein" }, Variants = new List<string> { "Кто автор \"Логико-философского трактата\", ключевого текста философии языка?", "Вопрос: назови автора \"Логико-философского трактата\"." }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "бодрийяр", "baudrillard" }, Variants = new List<string> { "Кто ввёл понятие \"симулякр\" в философию постмодернизма?", "Викторина: назови автора термина \"симулякр\"." }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "поппер", "popper" }, Variants = new List<string> { "Кто предложил опровержимость как критерий научности теории?", "Вопрос: назови автора критерия фальсифицируемости." }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "камю", "camus" }, Variants = new List<string> { "Кто автор \"Мифа о Сизифе\"?", "Викторина: назови автора \"Мифа о Сизифе\"." }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "эпикурейство", "эпикуреизм", "epicureanism" }, Variants = new List<string> { "Как называется школа, считающая удовольствие высшим благом, основанная одноимённым греком?", "Вопрос: назови школу, основанную Эпикуром." }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "франкл", "frankl" }, Variants = new List<string> { "Кто ввёл понятие \"экзистенциальный вакуум\" и написал \"Сказать жизни ДА\" после лагерей?", "Викторина: назови автора логотерапии и книги о лагерях." }},
                
new Question { Category = "Философия", Difficulty = "Средний", Answer = new List<string> { "кант", "kant" }, Variants = new List<string> { "Кто разделил реальность на \"вещь в себе\" и то, как мы её воспринимаем?", "Вопрос: назови автора понятия \"вещь в себе\"." }},

                // Философия -- сложный
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "фома аквинский", "aquinas" }, Variants = new List<string> { "Какой средневековый теолог написал \"Сумму теологии\"?", "Викторина: назови автора \"Суммы теологии\"." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "шопенгауэр", "schopenhauer" }, Variants = new List<string> { "Кто ввёл понятие \"воля к жизни\" как метафизическую основу мира, предвосхитив Ницше?", "Вопрос: назови автора понятия \"воля к жизни\"." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "лейбниц", "leibniz" }, Variants = new List<string> { "Кто разработал \"монадологию\" — учение о простых неделимых субстанциях?", "Викторина: назови автора монадологии." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "кун", "kuhn" }, Variants = new List<string> { "Кто ввёл в философию науки понятие \"смены парадигм\"?", "Вопрос: назови автора понятия \"смена парадигм\"." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "демокрит", "democritus" }, Variants = new List<string> { "Какой древнегреческий философ утверждал, что всё состоит из неделимых атомов?", "Викторина: назови философа-основателя атомизма." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "зенон элейский", "zeno of elea" }, Variants = new List<string> { "Кто автор апорий о движении, включая парадокс Ахиллеса и черепахи?", "Вопрос: назови автора апории про Ахиллеса и черепаху." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "хайдеггер", "heidegger" }, Variants = new List<string> { "Кто ввёл понятие \"бытие-к-смерти\" в философии существования?", "Викторина: назови автора понятия \"бытие-к-смерти\"." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "кун", "kuhn" }, Variants = new List<string> { "Кто автор книги \"Структура научных революций\"?", "Вопрос: назови автора \"Структуры научных революций\"." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "деррида", "derrida" }, Variants = new List<string> { "Какой французский философ ввёл понятие \"деконструкции\"?", "Викторина: назови автора термина \"деконструкция\"." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "фуко", "foucault" }, Variants = new List<string> { "Кто разработал понятие \"паноптикума\" как метафоры дисциплинарного общества?", "Вопрос: назови автора метафоры паноптикума." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "пирс", "peirce" }, Variants = new List<string> { "Кто считается основателем прагматизма как философского направления в США?", "Викторина: назови основателя американского прагматизма." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "гегель", "hegel" }, Variants = new List<string> { "Кто автор \"Феноменологии духа\"?", "Вопрос: назови автора \"Феноменологии духа\"." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "гуссерль", "husserl" }, Variants = new List<string> { "Кто ввёл понятие \"жизненного мира\" (Lebenswelt)?", "Викторина: назови автора понятия \"жизненный мир\"." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "ролз", "rawls" }, Variants = new List<string> { "Кто автор \"Теории справедливости\", определяющей справедливость как честность?", "Вопрос: назови автора \"Теории справедливости\"." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "платон", "plato" }, Variants = new List<string> { "Какой античный философ делил душу на разум, волю и вожделение?", "Викторина: назови философа с трёхчастной моделью души." }},
                
new Question { Category = "Философия", Difficulty = "Сложный", Answer = new List<string> { "франкл", "frankl" }, Variants = new List<string> { "Кто разработал логотерапию и понятие \"воли к смыслу\"?", "Вопрос: назови автора логотерапии." }},

                // Чай -- лёгкий
                

                // Финансовые загадки
new Question { Category = "Финансы", Answer = new List<string> { "денежный поток", "кэшфлоу", "cash flow", "ликвидност", "нет денег" }, Variants = new List<string>
                {
                    "Почему компания может показывать прибыль на бумаге и одновременно обанкротиться из-за нехватки денег?",
                    "Финансовая загадка: как компания с прибылью в отчётности вообще может обанкротиться от нехватки живых денег?",
                    "Вопрос дня: прибыль в бумагах есть, а денег на счету нет — как такое вообще возможно?",
                }},
                
new Question { Category = "Финансы", Answer = new List<string> { "падает", "падают", "дешевеют", "снижа" }, Variants = new List<string>
                {
                    "Что произойдёт с ценой облигации, если центробанк резко поднимет процентную ставку?",
                    "Загадка про облигации: центробанк резко поднял ставку — что в этот момент происходит с ценой уже выпущенных облигаций?",
                    "Вопрос дня: почему рост ставки центробанка бьёт по цене старых облигаций, а не по новым?",
                }},
                
new Question { Category = "Финансы", Answer = new List<string> { "в цене", "заложен", "наценк", "продавец платит" }, Variants = new List<string>
                {
                    "Почему \"бесплатный\" кредит на самом деле почти всегда кто-то оплачивает?",
                    "Финансовая загадка: если кредит рекламируют как бесплатный, кто на самом деле платит за эту \"бесплатность\"?",
                    "Вопрос дня: бывает ли действительно бесплатный кредит, или за него всегда платит кто-то другой?",
                }},
                
new Question { Category = "Финансы", Answer = new List<string> { "беднеешь", "теряешь", "минус", "реальная ставка отрицательн" }, Variants = new List<string>
                {
                    "Если инфляция 10% в год, а твой вклад приносит 8% — что реально происходит с твоими деньгами в реальном выражении?",
                    "Загадка: вклад даёт 8% годовых, инфляция — 10%. Как это сказывается на реальной покупательной способности денег?",
                    "Вопрос дня: можно ли считать себя богаче, если ставка по вкладу ниже инфляции?",
                }},
                
new Question { Category = "Финансы", Answer = new List<string> { "ожидани", "заложено в цену", "уже учтено", "хуже ожиданий" }, Variants = new List<string>
                {
                    "Почему акция компании может упасть в цене даже после отличного квартального отчёта?",
                    "Финансовая загадка: отчёт вышел отличный, а акции всё равно падают — как так?",
                    "Вопрос дня: компания отчиталась значительно лучше прогнозов аналитиков, но акции всё равно в минусе. Где логика?",
                }},
                
new Question { Category = "Финансы", Answer = new List<string> { "30", "тридцать" }, Variants = new List<string>
                {
                    "Что дороже в итоге — взять ипотеку на 15 лет или на 30 при той же ставке?",
                    "Загадка про ипотеку: ставка одна и та же, но что выгоднее в итоге — 15 лет платежей или 30?",
                    "Вопрос дня: одна ставка, разный срок — где переплата в итоге больше, на коротком сроке или на длинном?",
                }},
                
new Question { Category = "Финансы", Answer = new List<string> { "несистематическ", "специфическ", "усредняет" }, Variants = new List<string>
                {
                    "Почему диверсификация снижает риск, но не увеличивает ожидаемую доходность?",
                    "Финансовая загадка: если раскидать деньги по разным активам, риск падает — а доходность растёт или нет?",
                    "Вопрос дня: диверсификация спасает от риска, но почему она не делает тебя богаче в среднем?",
                }},
                
new Question { Category = "Финансы", Answer = new List<string> { "экспортёр", "экспортер", "должник" }, Variants = new List<string>
                {
                    "Если валюта страны резко девальвируется, кто выигрывает, а кто теряет в первую очередь?",
                    "Загадка про девальвацию: курс рухнул резко — кому это на руку, а кто теряет сразу же?",
                    "Вопрос дня: резкое падение курса нацвалюты — для кого это шанс, а для кого катастрофа?",
                }},
                
new Question { Category = "Финансы", Answer = new List<string> { "эйнштейн", "einstein" }, Variants = new List<string>
                {
                    "Почему \"сложный процент\" называют восьмым чудом света?",
                    "Финансовая загадка: чем сложный процент настолько особенный, что его называют восьмым чудом света?",
                    "Вопрос дня: что такого в обычном сложном проценте, раз его сравнивают с чудом света?",
                }},
                
new Question { Category = "Финансы", Answer = new List<string> { "да", "пузырь", "инерция", "моментум" }, Variants = new List<string>
                {
                    "Может ли актив одновременно быть переоценённым и продолжать расти в цене?",
                    "Загадка про рынок: актив явно переоценён — но при этом продолжает дорожать. Как одно уживается с другим?",
                    "Вопрос дня: если все согласны, что актив стоит слишком дорого, почему он всё равно растёт дальше?",
                }},

                // Компьютерная техника
                
new Question { Category = "Финансы", Answer = new List<string> { "падает", "падают", "дешевеют", "снижа" }, Variants = new List<string>
                {
                    "Викторина: как реагирует цена уже выпущенных облигаций на резкое повышение ставки центробанком?",
                    "Вопрос с ответом: что происходит с ценой облигации при резком повышении ставки центробанком?",
                    "На угадайку: как меняется цена старых облигаций при росте ключевой ставки?",
                }},
                
new Question { Category = "Финансы", Answer = new List<string> { "эйнштейн", "einstein" }, Variants = new List<string>
                {
                    "Викторина: кому обычно (хоть и не вполне доказанно) приписывают фразу о сложном проценте как восьмом чуде света?",
                    "Вопрос с ответом: какому знаменитому учёному приписывают цитату про сложный процент как восьмое чудо света?",
                    "На угадайку: чьё имя чаще всего всплывает рядом с фразой про сложный процент и восьмое чудо света?",
                }},
                
new Question { Category = "Финансы", Answer = new List<string> { "системный", "рыночный", "systematic" }, Variants = new List<string>
                {
                    "Викторина: как называется риск, который не устраняется диверсификацией портфеля, потому что затрагивает весь рынок целиком?",
                    "Вопрос с ответом: какой тип риска остаётся с инвестором даже после идеальной диверсификации?",
                    "На угадайку: назови риск, который диверсификация в принципе не может убрать.",
                }},
                
new Question { Category = "Финансы", Answer = new List<string> { "больше", "выше" }, Variants = new List<string>
                {
                    "Викторина: как соотносится итоговая переплата по процентам при ипотеке на 30 лет и на 15 лет при одинаковой ставке?",
                    "Вопрос с ответом: как меняется итоговая переплата по процентам с увеличением срока ипотеки при равной ставке?",
                    "На угадайку: сравни переплату по процентам между ипотекой на 30 лет и на 15 лет при одинаковой ставке.",
                }},

                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "сложный процент", "compound interest" }, Variants = new List<string> { "Как называется процент, начисляемый и на изначальную сумму, и на уже накопленные проценты?", "Викторина: назови процент, который начисляется сам на себя." }},
                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "акция", "stock" }, Variants = new List<string> { "Как называется документ, подтверждающий право на долю в компании?", "Вопрос: назови бумагу, дающую долю в компании." }},
                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "облигация", "bond" }, Variants = new List<string> { "Как называется долговая ценная бумага с фиксированным доходом?", "Викторина: назови долговую бумагу с фиксированным доходом." }},
                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "инфляция", "inflation" }, Variants = new List<string> { "Как называется общий рост цен в экономике со временем?", "Вопрос: назови общий рост цен в экономике." }},
                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "банкротство", "bankruptcy" }, Variants = new List<string> { "Как называется неспособность компании платить по долгам?", "Викторина: назови состояние неспособности платить по долгам." }},
                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "s&p 500", "s&p", "снп 500" }, Variants = new List<string> { "Как называется индекс 500 крупнейших компаний США?", "Вопрос: назови индекс 500 крупнейших компаний США." }},
                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "ключевая ставка", "key rate" }, Variants = new List<string> { "Как называется ставка, по которой центробанк кредитует коммерческие банки?", "Викторина: назови ставку центробанка для кредитования банков." }},
                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "инвестиция", "investment" }, Variants = new List<string> { "Как называется вложение денег с целью получения дохода?", "Вопрос: назови вложение денег ради дохода." }},
                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "спред", "spread" }, Variants = new List<string> { "Как называется разница между ценой покупки и продажи актива?", "Викторина: назови разницу между ценой покупки и продажи." }},
                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "диверсификация", "diversification" }, Variants = new List<string> { "Как называется распределение капитала между разными активами ради снижения риска?", "Вопрос: назови распределение капитала ради снижения риска." }},
                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "центральный банк", "central bank" }, Variants = new List<string> { "Как называется орган, определяющий денежно-кредитную политику страны?", "Викторина: назови орган денежно-кредитной политики страны." }},
                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "девальвация", "devaluation" }, Variants = new List<string> { "Как называется резкое обесценивание национальной валюты?", "Вопрос: назови резкое обесценивание валюты." }},
                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "лонг", "long" }, Variants = new List<string> { "Как называется покупка актива в расчёте на рост его цены?", "Викторина: назови позицию в расчёте на рост цены." }},
                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "межбанковская", "interbank" }, Variants = new List<string> { "Как называется ставка, по которой банки кредитуют друг друга?", "Вопрос: назови ставку кредитования между банками." }},
                
new Question { Category = "Финансы", Difficulty = "Лёгкий", Answer = new List<string> { "дивиденды", "dividends" }, Variants = new List<string> { "Как называется доля прибыли компании, выплачиваемая акционерам?", "Викторина: назови выплату акционерам из прибыли." }},

                // Финансы -- средний
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "понци", "ponzi" }, Variants = new List<string> { "Как называется финансовая пирамида, названная в честь мошенника начала XX века?", "Вопрос: назови пирамиду, названную в честь мошенника." }},
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "пузырь", "bubble" }, Variants = new List<string> { "Как называется рост цены актива за счёт одних лишь ожиданий дальнейшего роста?", "Викторина: назови рост цены на одних ожиданиях." }},
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "p/e", "пи е", "р/е" }, Variants = new List<string> { "Как называется коэффициент отношения цены акции к прибыли компании?", "Вопрос: назови коэффициент цена/прибыль." }},
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "арбитраж", "arbitrage" }, Variants = new List<string> { "Как называется одновременная покупка и продажа связанных активов ради безрисковой разницы в цене?", "Викторина: назови безрисковую игру на разнице цен." }},
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "инверсия кривой", "inverted yield" }, Variants = new List<string> { "Как называется ситуация, когда доходность коротких облигаций выше длинных — предвестник рецессии?", "Вопрос: назови предвестник рецессии на рынке облигаций." }},
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "dcf", "дисконтирование денежных потоков" }, Variants = new List<string> { "Как называется метод оценки компании через прогноз будущих денежных потоков?", "Викторина: назови метод оценки компании через будущие потоки." }},
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "кредитный риск", "credit risk" }, Variants = new List<string> { "Как называется риск того, что контрагент не выполнит обязательства по сделке?", "Вопрос: назови риск невыполнения обязательств контрагентом." }},
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "шорт", "short" }, Variants = new List<string> { "Как называется продажа актива, которым не владеешь, в расчёте на падение цены?", "Викторина: назови продажу актива в расчёте на падение цены." }},
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "слияние", "merger" }, Variants = new List<string> { "Как называется объединение компаний, при котором акционеры одной получают акции другой?", "Вопрос: назови объединение компаний через обмен акциями." }},
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "количественное смягчение", "qe" }, Variants = new List<string> { "Как называется печать денег центробанком для покупки активов и стимулирования экономики?", "Викторина: назови стимулирование экономики через печать денег." }},
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "vix" }, Variants = new List<string> { "Как называется индекс ожидаемой волатильности S&P 500, известный как \"индекс страха\"?", "Вопрос: назови \"индекс страха\" рынка." }},
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "маржин-колл", "margin call" }, Variants = new List<string> { "Как называется принудительное требование довнести средства при использовании заёмного плеча?", "Викторина: назови требование довнести средства по плечу." }},
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "инсайдерская торговля", "insider trading" }, Variants = new List<string> { "Как называется торговля акциями с использованием непубличной информации?", "Вопрос: назови торговлю на непубличной информации." }},
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "прогрессивный", "progressive tax" }, Variants = new List<string> { "Как называется налог, ставка которого растёт по мере роста дохода?", "Викторина: назови налог с растущей ставкой." }},
                
new Question { Category = "Финансы", Difficulty = "Средний", Answer = new List<string> { "эйнштейн", "einstein" }, Variants = new List<string> { "Кому обычно (хоть и не вполне доказанно) приписывают фразу о сложном проценте как восьмом чуде света?", "Вопрос: чьё имя связывают с фразой про восьмое чудо света?" }},

                // Финансы -- сложный
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "эффективный рынок", "efficient market" }, Variants = new List<string> { "Как называется гипотеза, утверждающая, что цена акции уже отражает всю доступную информацию?", "Викторина: назови гипотезу эффективного рынка." }},
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "блэк", "black" }, Variants = new List<string> { "Чьим именем (вместе с Шоулзом) названа знаменитая модель ценообразования опционов?", "Вопрос: назови соавтора Шоулза в знаменитой модели ценообразования опционов." }},
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "стадное поведение", "herd behavior" }, Variants = new List<string> { "Как называется иррациональное следование инвесторов за толпой вопреки фундаментальным показателям?", "Викторина: назови иррациональное следование инвесторов за толпой." }},
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "бета", "beta" }, Variants = new List<string> { "Как называется коэффициент чувствительности актива к движению всего рынка?", "Вопрос: назови коэффициент чувствительности к рынку." }},
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "субстандартный", "subprime" }, Variants = new List<string> { "Как назывался кризис 2008 года, вызванный дефолтами по ипотеке низкого качества в США?", "Викторина: назови тип ипотечного кризиса 2008 года." }},
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "поведенческие финансы", "behavioral finance" }, Variants = new List<string> { "Как называется направление, изучающее иррациональные искажения при принятии финансовых решений?", "Вопрос: назови направление о финансовых искажениях восприятия." }},
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "cdo" }, Variants = new List<string> { "Как называется структурированная бумага, объединяющая множество ипотечных кредитов?", "Викторина: назови структурированную бумагу из ипотечных кредитов." }},
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "талеб", "taleb" }, Variants = new List<string> { "Кто автор книги \"Чёрный лебедь\" о непредсказуемых редких событиях?", "Вопрос: назови автора \"Чёрного лебедя\"." }},
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "стагфляция", "stagflation" }, Variants = new List<string> { "Как называется сочетание высокой инфляции и стагнации экономики одновременно?", "Викторина: назови сочетание инфляции и стагнации." }},
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "отрицательная реальная ставка", "negative real rate" }, Variants = new List<string> { "Как называется ситуация, когда номинальная ставка ниже уровня инфляции?", "Вопрос: назови ситуацию, когда ставка ниже инфляции." }},
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "финансовый рычаг", "leverage" }, Variants = new List<string> { "Как называется соотношение заёмного капитала компании к собственному?", "Викторина: назови соотношение заёмного капитала к собственному." }},
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "валютный коридор", "currency peg" }, Variants = new List<string> { "Как называется удержание курса валюты в узком коридоре относительно другой валюты?", "Вопрос: назови удержание курса в узком коридоре." }},
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "большего дурака", "greater fool" }, Variants = new List<string> { "Как называется теория, объясняющая покупку переоценённого актива расчётом продать его дороже кому-то ещё?", "Викторина: назови теорию про перепродажу переоценённого актива." }},
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "долг больше ввп", "debt exceeds gdp" }, Variants = new List<string> { "Как называют ситуацию, когда госдолг страны превышает её годовой ВВП?", "Вопрос: как называется превышение госдолга над ВВП?" }},
                
new Question { Category = "Финансы", Difficulty = "Сложный", Answer = new List<string> { "доу-джонс", "dow jones" }, Variants = new List<string> { "Как называется один из старейших фондовых индексов США, объединяющий 30 промышленных гигантов?", "Викторина: назови старейший индекс из 30 промышленных компаний США." }},

                // Компьютеры -- лёгкий
                

                // Чай -- лёгкий
                new Question { Category = "Чай", Difficulty = "Лёгкий", Answer = new List<string> { "камелия китайская", "camellia sinensis" }, Variants = new List<string> { "Из какого растения делают практически весь настоящий чай (не травяные настои), независимо от сорта?", "Викторина: назови растение-источник настоящего чая." }},
                new Question { Category = "Чай", Difficulty = "Лёгкий", Answer = new List<string> { "зелёный чай", "green tea" }, Variants = new List<string> { "Как называется чай, который не проходит ферментацию и сохраняет исходный цвет свежего листа?", "Вопрос: назови чай без ферментации листа." }},
                new Question { Category = "Чай", Difficulty = "Лёгкий", Answer = new List<string> { "япония", "japan" }, Variants = new List<string> { "Из какой страны родом чайная церемония тя-но-ю?", "Викторина: назови родину чайной церемонии тя-но-ю." }},
                new Question { Category = "Чай", Difficulty = "Лёгкий", Answer = new List<string> { "исин", "yixing" }, Variants = new List<string> { "Как называется небольшой глиняный чайник для традиционного китайского заваривания, названный в честь города-производителя?", "Вопрос: назови тип глиняного чайника из Китая, названный по городу." }},
                new Question { Category = "Чай", Difficulty = "Лёгкий", Answer = new List<string> { "юньнань", "yunnan" }, Variants = new List<string> { "Из какой провинции Китая родом знаменитый прессованный чай, который выдерживают десятилетиями, как вино?", "Викторина: назови провинцию-родину выдержанного прессованного чая." }},
                new Question { Category = "Чай", Difficulty = "Лёгкий", Answer = new List<string> { "гайвань", "gaiwan" }, Variants = new List<string> { "Как называется маленькая чашка-крышка без ручки, часто заменяющая чайник в Китае?", "Вопрос: назови сосуд-крышку для заваривания без ручки." }},
                new Question { Category = "Чай", Difficulty = "Лёгкий", Answer = new List<string> { "белый чай", "white tea" }, Variants = new List<string> { "Как называется наименее обработанный чай, состоящий в основном из молодых почек с ворсинками?", "Викторина: назови чай из молодых почек с минимальной обработкой." }},
                new Question { Category = "Чай", Difficulty = "Лёгкий", Answer = new List<string> { "улун", "oolong" }, Variants = new List<string> { "Как называется чай с частичной ферментацией — между зелёным и полностью ферментированным?", "Вопрос: назови чай с промежуточной ферментацией." }},
                new Question { Category = "Чай", Difficulty = "Лёгкий", Answer = new List<string> { "китай", "china" }, Variants = new List<string> { "Какая страна дольше всех в мире выращивает и производит чай как культурную традицию?", "Викторина: назови страну-родину чайной культуры." }},
                new Question { Category = "Чай", Difficulty = "Лёгкий", Answer = new List<string> { "да" }, Variants = new List<string> { "Правда ли, что хороший лист улуна можно заваривать больше десяти раз подряд без потери вкуса?", "Вопрос: можно ли качественный улун заваривать многократно, десять раз и больше?" }},

                // Чай -- средний
                new Question { Category = "Чай", Difficulty = "Средний", Answer = new List<string> { "фиксация" }, Variants = new List<string> { "Как называется остановка окисления чайного листа нагревом — ключевой этап обработки зелёного чая?", "Викторина: назови этап остановки окисления листа нагревом." }},
                new Question { Category = "Чай", Difficulty = "Средний", Answer = new List<string> { "скручивание", "rolling" }, Variants = new List<string> { "Как называется этап ручного придания формы чайному листу, влияющий на скорость и характер заваривания?", "Вопрос: назови этап формовки чайного листа руками после сушки." }},
                new Question { Category = "Чай", Difficulty = "Средний", Answer = new List<string> { "жёлтый чай", "желтый чай", "yellow tea" }, Variants = new List<string> { "Как называется редкая категория чая с дополнительной лёгкой ферментацией в тепле, стоящая между зелёным и улуном?", "Викторина: назови редкую категорию между зелёным чаем и улуном." }},
                new Question { Category = "Чай", Difficulty = "Средний", Answer = new List<string> { "чабань" }, Variants = new List<string> { "Как называется набор-поднос, специально предназначенный для многократных коротких проливов чая?", "Вопрос: назови поднос для многократного заваривания." }},
                new Question { Category = "Чай", Difficulty = "Средний", Answer = new List<string> { "промывка" }, Variants = new List<string> { "Как называется первый быстрый пролив кипятком, который принято сливать, не выпивая?", "Викторина: назови первый пролив чая, который сливают." }},
                new Question { Category = "Чай", Difficulty = "Средний", Answer = new List<string> { "шэн пуэр", "sheng" }, Variants = new List<string> { "Как называется категория пуэра, ферментирующаяся естественным путём годами, без ускорения?", "Вопрос: назови категорию пуэра с естественной многолетней ферментацией." }},
                new Question { Category = "Чай", Difficulty = "Средний", Answer = new List<string> { "шу пуэр", "shou", "shu" }, Variants = new List<string> { "Как называется категория пуэра, подвергнутая ускоренной искусственной ферментации за недели вместо лет?", "Викторина: назови категорию пуэра с искусственно ускоренной ферментацией." }},
                new Question { Category = "Чай", Difficulty = "Средний", Answer = new List<string> { "красный чай", "хунча" }, Variants = new List<string> { "Как китайская классификация называет чай, который на Западе принято называть \"чёрным\"?", "Вопрос: назови китайское название того, что на Западе зовут чёрным чаем." }},
                new Question { Category = "Чай", Difficulty = "Средний", Answer = new List<string> { "фуцзянь", "fujian" }, Variants = new List<string> { "Из какой провинции родом утёсные улуны вроде да хун пао?", "Викторина: назови провинцию утёсных улунов." }},
                new Question { Category = "Чай", Difficulty = "Средний", Answer = new List<string> { "да" }, Variants = new List<string> { "Правда ли, что зелёный чай традиционно заваривают менее горячей водой, чем улун или пуэр?", "Вопрос: заваривают ли зелёный чай менее горячей водой, чем улун?" }},

                // Чай -- сложный
                new Question { Category = "Чай", Difficulty = "Сложный", Answer = new List<string> { "да хун пао", "da hong pao" }, Variants = new List<string> { "Как называется самый дорогой утёсный улун из гор Уишань, чьё имя переводится как \"большой красный халат\"?", "Викторина: назови легендарный улун, чьё имя переводится как большой красный халат." }},
                new Question { Category = "Чай", Difficulty = "Сложный", Answer = new List<string> { "скентинг", "scenting" }, Variants = new List<string> { "Как называется техника ароматизации чая свежим жасмином на ночь с последующим удалением цветов?", "Вопрос: назови технику ароматизации чая жасмином." }},
                new Question { Category = "Чай", Difficulty = "Сложный", Answer = new List<string> { "фуцзянь", "fujian" }, Variants = new List<string> { "В какой провинции производят знаменитый белый чай \"Серебряные иглы\" (Бай Хао Инь Чжэнь)?", "Викторина: назови провинцию белого чая \"Серебряные иглы\"." }},
                new Question { Category = "Чай", Difficulty = "Сложный", Answer = new List<string> { "цзянсу", "jiangsu" }, Variants = new List<string> { "Из какой провинции родом знаменитый зелёный чай Би Ло Чунь (\"Изумрудные спирали весны\")?", "Вопрос: назови провинцию чая \"Изумрудные спирали весны\"." }},
                new Question { Category = "Чай", Difficulty = "Сложный", Answer = new List<string> { "прожарка", "roasting" }, Variants = new List<string> { "Как называется обжарка чайного листа на углях, часто применяемая к утёсным улунам?", "Викторина: назови технику обжарки улуна на углях." }},
                new Question { Category = "Чай", Difficulty = "Сложный", Answer = new List<string> { "цзыша" }, Variants = new List<string> { "Как называется исинская глина по составу, впитывающая ароматы чая со временем?", "Вопрос: назови тип исинской глины для чайников." }},
                new Question { Category = "Чай", Difficulty = "Сложный", Answer = new List<string> { "хуэй гань" }, Variants = new List<string> { "Как называется термин для \"возвращающейся сладости\" послевкусия, особенно ценимой в улунах?", "Викторина: назови термин для сладкого послевкусия чая." }},
                new Question { Category = "Чай", Difficulty = "Сложный", Answer = new List<string> { "цзюнь шань", "junshan" }, Variants = new List<string> { "Как называется один из немногих сохранившихся жёлтых чаёв, родом из провинции Хунань?", "Вопрос: назови знаменитый жёлтый чай из Хунани." }},
                new Question { Category = "Чай", Difficulty = "Сложный", Answer = new List<string> { "влажное хранение", "wet storage" }, Variants = new List<string> { "Как называется хранение пуэра во влажном климате для намеренного ускорения ферментации?", "Викторина: назови метод хранения пуэра для ускорения выдержки." }},
                new Question { Category = "Чай", Difficulty = "Сложный", Answer = new List<string> { "лу юй", "lu yu" }, Variants = new List<string> { "Кто написал \"Ча цзин\" (\"Канон чая\") в эпоху Тан — первый системный трактат о чае?", "Вопрос: назови автора первого системного трактата о чае." }},
                // Машины (Lexus) -- лёгкий
                new Question { Category = "Машины", Difficulty = "Лёгкий", Answer = new List<string> { "япония", "japan" }, Variants = new List<string> { "Какая страна является родиной бренда Lexus?", "Викторина: назови страну-родину Lexus." }},
                new Question { Category = "Машины", Difficulty = "Лёгкий", Answer = new List<string> { "toyota", "тойота" }, Variants = new List<string> { "Какой концерн владеет брендом Lexus?", "Вопрос: назови материнскую компанию Lexus." }},
                new Question { Category = "Машины", Difficulty = "Лёгкий", Answer = new List<string> { "1989" }, Variants = new List<string> { "В каком году бренд Lexus официально вышел на рынок США?", "Викторина: назови год выхода Lexus на рынок США." }},
                new Question { Category = "Машины", Difficulty = "Лёгкий", Answer = new List<string> { "ls 400" }, Variants = new List<string> { "Как называлась первая модель Lexus, представленная в 1989 году?", "Вопрос: назови первую модель Lexus." }},
                new Question { Category = "Машины", Difficulty = "Лёгкий", Answer = new List<string> { "ls" }, Variants = new List<string> { "Как называется флагманский седан Lexus, топовая модель бренда?", "Викторина: назови флагманский седан Lexus." }},
                new Question { Category = "Машины", Difficulty = "Лёгкий", Answer = new List<string> { "rx" }, Variants = new List<string> { "Как называется среднеразмерный кроссовер Lexus, один из самых продаваемых в линейке?", "Вопрос: назови самый продаваемый кроссовер Lexus." }},
                new Question { Category = "Машины", Difficulty = "Лёгкий", Answer = new List<string> { "lexus f" }, Variants = new List<string> { "Как называется спортивное подразделение Lexus, аналог AMG у Mercedes?", "Викторина: назови спортивное подразделение Lexus." }},
                new Question { Category = "Машины", Difficulty = "Лёгкий", Answer = new List<string> { "ux" }, Variants = new List<string> { "Как называется самый компактный кроссовер Lexus, младше NX?", "Вопрос: назови самый маленький кроссовер Lexus." }},
                new Question { Category = "Машины", Difficulty = "Лёгкий", Answer = new List<string> { "lx" }, Variants = new List<string> { "Как называется топовый внедорожник Lexus, построенный на платформе Land Cruiser?", "Викторина: назови топовый внедорожник Lexus на платформе Land Cruiser." }},
                new Question { Category = "Машины", Difficulty = "Лёгкий", Answer = new List<string> { "is" }, Variants = new List<string> { "Как называется входной, самый доступный седан в линейке Lexus?", "Вопрос: назови начальный седан Lexus." }},

                // Машины (Lexus) -- средний
                new Question { Category = "Машины", Difficulty = "Средний", Answer = new List<string> { "hybrid synergy drive" }, Variants = new List<string> { "Как называется гибридная технология Toyota/Lexus, впервые массово внедрённая в конце 1990-х?", "Викторина: назови раннюю гибридную технологию Toyota/Lexus." }},
                new Question { Category = "Машины", Difficulty = "Средний", Answer = new List<string> { "avs", "авс" }, Variants = new List<string> { "Как называется технология Lexus, регулирующая жёсткость амортизаторов в реальном времени?", "Вопрос: назови технологию адаптивной подвески Lexus." }},
                new Question { Category = "Машины", Difficulty = "Средний", Answer = new List<string> { "spindle grille", "веретенообразная" }, Variants = new List<string> { "Как называется фирменная форма решётки радиатора Lexus, похожая на песочные часы?", "Викторина: назови фирменную форму решётки радиатора Lexus." }},
                new Question { Category = "Машины", Difficulty = "Средний", Answer = new List<string> { "e-four" }, Variants = new List<string> { "Как называется система полного привода Lexus с электромотором на задней оси вместо механического привода?", "Вопрос: назови электрический полный привод Lexus." }},
                new Question { Category = "Машины", Difficulty = "Средний", Answer = new List<string> { "road sign assist" }, Variants = new List<string> { "Как называется технология Lexus, распознающая дорожные знаки и выводящая ограничение скорости на приборку?", "Викторина: назови технологию распознавания знаков Lexus." }},
                new Question { Category = "Машины", Difficulty = "Средний", Answer = new List<string> { "lexus safety system" }, Variants = new List<string> { "Как называется пакет активных систем безопасности Lexus, включающий круиз-контроль и удержание полосы?", "Вопрос: назови пакет активной безопасности Lexus." }},
                new Question { Category = "Машины", Difficulty = "Средний", Answer = new List<string> { "f sport" }, Variants = new List<string> { "Как называется более доступный спортивный пакет отделки Lexus, отличающийся от полноценных топовых моделей F?", "Викторина: назови спортивный пакет отделки Lexus рангом ниже полного F." }},
                new Question { Category = "Машины", Difficulty = "Средний", Answer = new List<string> { "ls 500h" }, Variants = new List<string> { "Как называется гибридная версия флагманского седана Lexus LS 500?", "Вопрос: назови гибридную версию Lexus LS 500." }},
                new Question { Category = "Машины", Difficulty = "Средний", Answer = new List<string> { "нет", "миф" }, Variants = new List<string> { "Правда ли, что название \"Lexus\" официально расшифровывается как аббревиатура \"Luxury Export to the United States\"?", "Викторина: официально ли Lexus расшифровывается как \"Luxury Export to the United States\"?" }},
                new Question { Category = "Машины", Difficulty = "Средний", Answer = new List<string> { "alexis" }, Variants = new List<string> { "Какое имя было первоначальной рабочей идеей, прежде чем его сократили и видоизменили до \"Lexus\"?", "Вопрос: назови рабочее имя-прототип, из которого получилось \"Lexus\"." }},

                // Машины (Lexus) -- сложный
                new Question { Category = "Машины", Difficulty = "Сложный", Answer = new List<string> { "январь" }, Variants = new List<string> { "В каком месяце 1989 года Lexus LS 400 впервые показали публике на автошоу в Детройте?", "Викторина: назови месяц премьеры Lexus LS 400 в Детройте." }},
                new Question { Category = "Машины", Difficulty = "Сложный", Answer = new List<string> { "август" }, Variants = new List<string> { "В каком месяце 1989 года автомобили Lexus LS 400 впервые поступили в продажу, спустя несколько месяцев после презентации?", "Вопрос: назови месяц начала продаж Lexus LS 400 в 1989 году." }},
                new Question { Category = "Машины", Difficulty = "Сложный", Answer = new List<string> { "lf-gh" }, Variants = new List<string> { "Как называется концепт-кар 2011 года, на котором Lexus впервые показал новую форму решётки радиатора?", "Викторина: назови концепт-кар 2011 года с первой веретенообразной решёткой Lexus." }},
                new Question { Category = "Машины", Difficulty = "Сложный", Answer = new List<string> { "gs" }, Variants = new List<string> { "Какая серийная модель Lexus первой получила новую форму решётки радиатора в 2012 году?", "Вопрос: назови первую серийную модель Lexus с новой решёткой." }},
                new Question { Category = "Машины", Difficulty = "Сложный", Answer = new List<string> { "нет" }, Variants = new List<string> { "Правда ли, что оригинальный Lexus LS 400 1989 года уже имел фирменную решётку радиатора в форме веретена?", "Викторина: была ли у первого Lexus LS 400 веретенообразная решётка?" }},
                new Question { Category = "Машины", Difficulty = "Сложный", Answer = new List<string> { "lippincott" }, Variants = new List<string> { "Какая компания-консультант по неймингу предложила Toyota более 200 вариантов названий для нового бренда?", "Вопрос: назови нейминговое агентство, придумавшее варианты для будущего Lexus." }},
                new Question { Category = "Машины", Difficulty = "Сложный", Answer = new List<string> { "lexicon" }, Variants = new List<string> { "Какое греческое слово, означающее \"язык, словарь\", легло в основу имени Lexus?", "Викторина: назови греческое слово-основу имени Lexus." }},
                new Question { Category = "Машины", Difficulty = "Сложный", Answer = new List<string> { "luxus" }, Variants = new List<string> { "Какое латинское слово, означающее \"роскошь\", легло в основу имени Lexus вместе с греческим \"lexicon\"?", "Вопрос: назови латинское слово-основу имени Lexus." }},
                new Question { Category = "Машины", Difficulty = "Сложный", Answer = new List<string> { "детройт", "detroit" }, Variants = new List<string> { "На автошоу в каком американском городе дебютировал Lexus LS 400 в январе 1989 года?", "Викторина: назови город премьеры Lexus LS 400." }},
                new Question { Category = "Машины", Difficulty = "Сложный", Answer = new List<string> { "gx" }, Variants = new List<string> { "Как называется внедорожник Lexus, построенный на платформе Toyota Land Cruiser Prado — меньше, чем LX?", "Вопрос: назови внедорожник Lexus на платформе Land Cruiser Prado." }},
                // Рэп (история русского рэпа) -- лёгкий
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "мирон фёдоров", "фёдоров", "fedorov" }, Variants = new List<string> { "Как настоящее имя рэпера Oxxxymiron?", "Викторина: назови настоящее имя Oxxxymiron." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "иван дрёмин", "дрёмин", "дремин" }, Variants = new List<string> { "Как настоящее имя рэпера Face?", "Вопрос: назови настоящее имя рэпера Face." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "василий вакуленко", "вакуленко" }, Variants = new List<string> { "Как настоящее имя рэпера Басты?", "Викторина: назови настоящее имя Басты." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "ноггано" }, Variants = new List<string> { "Как называется более жёсткий и провокационный альтернативный псевдоним Басты?", "Вопрос: назови альтер-эго Басты с более жёстким звучанием." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "уфа" }, Variants = new List<string> { "В каком городе родился и вырос рэпер Face?", "Викторина: назови родной город рэпера Face." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "гоша рубчинский" }, Variants = new List<string> { "Какой хит-трек принёс рэперу Face широкую известность?", "Вопрос: назови трек, прославивший Face." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "слава кпсс", "гнойный" }, Variants = new List<string> { "Как звали оппонента Oxxxymiron в самом резонансном рэп-баттле 2017 года?", "Викторина: назови соперника Oxxxymiron в баттле 2017 года." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "2015" }, Variants = new List<string> { "В каком году вышел концептуальный альбом Oxxxymiron \"Горгород\"?", "Вопрос: назови год выхода альбома \"Горгород\"." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "ростов-на-дону", "ростов" }, Variants = new List<string> { "В каком городе была основана группа Каста в 1999 году?", "Викторина: назови город основания группы Каста." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "ленинград", "санкт-петербург" }, Variants = new List<string> { "В каком городе была основана в 1989 году первая российская рэп-группа Bad Balance?", "Вопрос: назови город основания Bad Balance." }},

                // Рэп (артисты и лейблы) -- лёгкий
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "адиль жалелов", "жалелов" }, Variants = new List<string> { "Как настоящее имя рэпера Скриптонит?", "Викторина: назови настоящее имя Скриптонита." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "казахстан" }, Variants = new List<string> { "В какой стране родился рэпер Скриптонит?", "Вопрос: назови страну рождения Скриптонита." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "дмитрий кузнецов", "кузнецов" }, Variants = new List<string> { "Как настоящее имя рэпера Хаски?", "Викторина: назови настоящее имя рэпера Хаски." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "андрей меньшиков", "меньшиков" }, Variants = new List<string> { "Как настоящее имя рэпера Лигалайз?", "Вопрос: назови настоящее имя Лигалайза." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "кирилл толмацкий", "толмацкий" }, Variants = new List<string> { "Как настоящее имя рэпера Децл?", "Викторина: назови настоящее имя Децла." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "2019" }, Variants = new List<string> { "В каком году не стало рэпера Децла?", "Вопрос: назови год смерти Децла." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "мальчишник" }, Variants = new List<string> { "В составе какой скандальной группы 90-х начинал будущий сольный музыкант Дельфин, прежде чем уйти в 1996 году?", "Викторина: назови группу, в которой состоял Дельфин до сольной карьеры." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "тимур юнусов", "юнусов" }, Variants = new List<string> { "Как настоящее имя рэпера Тимати?", "Вопрос: назови настоящее имя Тимати." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "2006" }, Variants = new List<string> { "В каком году Тимати основал лейбл Black Star?", "Викторина: назови год основания лейбла Black Star Тимати." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "устименко" }, Variants = new List<string> { "Как настоящая фамилия рэпера Джиган?", "Вопрос: назови настоящую фамилию Джигана." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "матвей мельников", "мельников" }, Variants = new List<string> { "Как настоящее имя рэпера Мот?", "Викторина: назови настоящее имя рэпера Мот." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "глеб голубин", "голубин" }, Variants = new List<string> { "Как настоящее имя рэпера Pharaoh?", "Вопрос: назови настоящее имя рэпера Pharaoh." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "александр жвакин", "жвакин" }, Variants = new List<string> { "Как настоящее имя рэпера Loc-Dog?", "Викторина: назови настоящее имя Loc-Dog." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "олег нечипоренко", "нечипоренко" }, Variants = new List<string> { "Как настоящее имя рэпера Kizaru?", "Вопрос: назови настоящее имя Kizaru." }},
                new Question { Category = "Рэп", Difficulty = "Лёгкий", Answer = new List<string> { "фёдор инсаров", "федор инсаров", "инсаров" }, Variants = new List<string> { "Как настоящее имя рэпера Feduk?", "Викторина: назови настоящее имя Feduk." }},

                // Рэп (история русского рэпа) -- средний
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "александр астров", "астров" }, Variants = new List<string> { "Кто записал первый рэп-альбом на русском языке в декабре 1983 года вместе с рок-группой \"Час пик\"?", "Викторина: назови автора первого русскоязычного рэп-альбома 1983 года." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "куйбышев" }, Variants = new List<string> { "Как назывался до 1991 года город Самара, где в 1983 году записали первый русскоязычный рэп-альбом?", "Вопрос: назови советское название Самары, где родился русский рэп." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "rap music" }, Variants = new List<string> { "Как назывался первый крупный фестиваль баттл-рэпа в России, прошедший 12 декабря 1994 года?", "Викторина: назови фестиваль баттл-рэпа 1994 года." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "психолирик" }, Variants = new List<string> { "Из какой группы вышли будущие участники Касты, до смены названия?", "Вопрос: назови группу-предшественницу Касты." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "центр" }, Variants = new List<string> { "Как назывался проект, который Гуф основал в 2003 году вместе с Принципом?", "Викторина: назови группу Гуфа и Принципа с 2003 года." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "вячеслав машнов", "машнов" }, Variants = new List<string> { "Как настоящее имя оппонента Oxxxymiron в баттле 2017 года?", "Вопрос: назови настоящее имя Славы КПСС." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "краснодар" }, Variants = new List<string> { "В каком городе была основана офлайн баттл-площадка Slovo?", "Викторина: назови город основания площадки Slovo." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "2013" }, Variants = new List<string> { "В каком году появилась баттл-площадка Versus Battle?", "Вопрос: назови год основания Versus Battle." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "easyrap" }, Variants = new List<string> { "Как называлась серия роликов 2017 года, благодаря которой Моргенштерн впервые получил известность?", "Викторина: назови серию роликов, прославившую Моргенштерна в 2017 году." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "егор ракитин", "ракитин" }, Variants = new List<string> { "Как настоящее имя рэпера Big Baby Tape?", "Вопрос: назови настоящее имя Big Baby Tape." }},

                // Рэп (артисты и лейблы) -- средний
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "леван горозия", "горозия" }, Variants = new List<string> { "Как настоящее имя рэпера L'One, вернувшегося к нему после ухода с лейбла Black Star в 2019 году?", "Вопрос: назови настоящее имя L'One." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "одесса" }, Variants = new List<string> { "В каком городе родился рэпер Джиган в 1985 году?", "Викторина: назови родной город Джигана." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "13" }, Variants = new List<string> { "До какого возраста будущий рэпер Pharaoh профессионально играл в футбол за юношеские команды Локомотива, ЦСКА и Динамо, прежде чем уйти в музыку?", "Вопрос: назови возраст, в котором Pharaoh бросил профессиональный футбол ради музыки." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "one piece", "ван пис" }, Variants = new List<string> { "Из какого культового аниме-сериала Kizaru взял имя адмирала для своего сценического псевдонима?", "Викторина: назови аниме, давшее имя псевдониму Kizaru." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "bioshock", "биошок" }, Variants = new List<string> { "Из какой видеоигры Booker позаимствовал имя персонажа для сценического псевдонима?", "Вопрос: назови видеоигру, давшую имя псевдониму Booker." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "роберт" }, Variants = new List<string> { "Как звали (имя) алхимика XVI-XVII веков, чья фамилия стала второй частью псевдонима GONE.Fludd?", "Викторина: назови имя алхимика, давшего фамилию для псевдонима GONE.Fludd." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "2015" }, Variants = new List<string> { "В каком году Jeembo присоединился к творческому объединению Dead Dynasty?", "Вопрос: назови год, когда Jeembo вошёл в состав Dead Dynasty." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "витебск" }, Variants = new List<string> { "В каком белорусском городе родился Олег Савченко, известный как ЛСП?", "Викторина: назови родной город ЛСП." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "2013" }, Variants = new List<string> { "В каком году Мот присоединился к лейблу Black Star?", "Вопрос: назови год, когда Мот стал артистом Black Star." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "вадим мотылёв", "вадим мотылев", "мотылёв", "мотылев" }, Variants = new List<string> { "Как настоящее имя рэпера Slim, фронтмена группы «Многоточие»?", "Викторина: назови настоящее имя Slim." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "кирилл незборецкий", "незборецкий" }, Variants = new List<string> { "Как настоящее имя рэпера T-Fest?", "Вопрос: назови настоящее имя T-Fest." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "владислав лешкевич", "лешкевич" }, Variants = new List<string> { "Как настоящее имя Влади, участника группы Каста?", "Викторина: назови настоящее имя Влади из Касты." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "татарский" }, Variants = new List<string> { "На каком языке исполнен хит рэперши Tatarka «Алтын», прославивший её в 2017 году?", "Вопрос: назови язык хита Tatarka «Алтын»." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "курточка твоя" }, Variants = new List<string> { "Какой трек принёс известность рэперше NAT (Анастасии Пискуновой) и привёл её на лейбл рэпера ST?", "Викторина: назови трек, прославивший рэпершу NAT." }},
                new Question { Category = "Рэп", Difficulty = "Средний", Answer = new List<string> { "саша чест" }, Variants = new List<string> { "Какой ещё артист, помимо T-Fest, пополнил лейбл Газгольдер в 2017 году?", "Вопрос: назови артиста, который вместе с T-Fest пришёл на Газгольдер в 2017 году." }},

                // Рэп (история русского рэпа) -- сложный
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "шило", "фельдман", "черняк", "фаин" }, Variants = new List<string> { "Кем были два главных участника-основателя проекта Кровосток, помимо диджея Полутрупа?", "Викторина: назови двух главных основателей Кровостока." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "иезекииль" }, Variants = new List<string> { "Как называлась группа 25/17 до 2009 года?", "Вопрос: назови прежнее название группы 25/17." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "омск" }, Variants = new List<string> { "Из какого города российские музыканты Бледный и Ант, основавшие группу 25/17?", "Викторина: назови родной город группы 25/17." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "ресторатор", "тимарцев" }, Variants = new List<string> { "Кто основал баттл-платформу Versus Battle в 2013 году?", "Вопрос: назови основателя Versus Battle." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "2018" }, Variants = new List<string> { "В каком году вышел дебютный альбом Big Baby Tape \"Dragonborn\"?", "Викторина: назови год выхода альбома \"Dragonborn\"." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "warner" }, Variants = new List<string> { "Какой крупный лейбл выпустил дебютный альбом Big Baby Tape \"Dragonborn\"?", "Вопрос: назови лейбл, выпустивший \"Dragonborn\"." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "1989" }, Variants = new List<string> { "В каком году была основана первая российская рэп-группа Bad Balance?", "Викторина: назови год основания Bad Balance." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "валов", "шеф" }, Variants = new List<string> { "Как звали основателя и лидера группы Bad Balance?", "Вопрос: назови лидера и основателя Bad Balance." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "трёхмерные рифмы" }, Variants = new List<string> { "Как назывался дебютный альбом Касты, который группа распространяла после победы на фестивале Rap Music в 1999 году?", "Викторина: назови дебютный альбом Касты 1999 года." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "slovo" }, Variants = new List<string> { "Какая офлайн-площадка для рэп-баттлов появилась в России первой, в 2012 году в Краснодаре?", "Вопрос: назови первую офлайн баттл-площадку России." }},

                // Рэп (артисты и лейблы) -- сложный
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "сергей крылов", "крылов" }, Variants = new List<string> { "Как настоящее имя диджея Полутрупа из группы Кровосток?", "Викторина: назови настоящее имя диджея Полутрупа." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "павел ивлев", "ивлев" }, Variants = new List<string> { "Как настоящее имя рэпера Паша Техник?", "Вопрос: назови настоящее имя Паши Техника." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "kunteynir", "контейнер" }, Variants = new List<string> { "Как называлась рэп-группа, которую Паша Техник основал в 2002 году вместе с МС Смешным?", "Викторина: назови группу Паши Техника и МС Смешного 2002 года." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "прикладная этика", "философия" }, Variants = new List<string> { "На каком факультете и по какой специальности учился баттл-рэпер Booker в СПбГУ?", "Вопрос: назови специальность Booker в СПбГУ." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "полярный" }, Variants = new List<string> { "В каком городе Мурманской области родился будущий рэпер GONE.Fludd?", "Викторина: назови город рождения GONE.Fludd." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "евангелие от собаки" }, Variants = new List<string> { "Как должен был называться концептуальный альбом Хаски об Иисусе в Москве, частью которого задумывалась песня «Иуда»?", "Вопрос: назови концептуальный альбом Хаски, в который должна была войти песня «Иуда»." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "12" }, Variants = new List<string> { "На сколько суток административного ареста отправили Хаски после того, как он забрался на крышу машины у клуба в Краснодаре в 2018 году?", "Викторина: назови срок административного ареста Хаски в Краснодаре." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "o.g. original gangster", "original gangster" }, Variants = new List<string> { "В честь какого альбома рэпера Ice-T образован первый слог «Джи-» в псевдониме «Джиган»?", "Вопрос: назови альбом Ice-T, давший первый слог псевдониму «Джиган»." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "2020" }, Variants = new List<string> { "27 июля какого года Тимати вышел из состава учредителей группы компаний Black Star?", "Викторина: назови год выхода Тимати из учредителей Black Star." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "марс" }, Variants = new List<string> { "Путешествие космонавта на какую планету символически описывает трилогия альбомов L'One 2013-2016 годов?", "Вопрос: назови планету из космической трилогии альбомов L'One." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "гуф" }, Variants = new List<string> { "Slim, впоследствии фронтмен «Многоточия», был участником той же группы «Центр», что и ещё один известный рэпер — кто это?", "Викторина: назови рэпера, который вместе со Slim состоял в группе «Центр»." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "пушкинг" }, Variants = new List<string> { "В каком арт-клубе Шило и Фельдман из Кровостока познакомились с диджеем Полутрупом, работавшим там барменом?", "Вопрос: назови арт-клуб, где будущие участники Кровостока познакомились с Полутрупом." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "мурасса уршанова", "уршанова" }, Variants = new List<string> { "Как настоящее имя Тати, бывшей участницы творческого объединения Газгольдер?", "Викторина: назови настоящее имя Тати." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "анастасия морозова", "морозова" }, Variants = new List<string> { "Как зовут самую юную участницу творческого объединения Газгольдер, выступающую под именем Baby Cute?", "Вопрос: назови настоящее имя Baby Cute." }},
                new Question { Category = "Рэп", Difficulty = "Сложный", Answer = new List<string> { "2025" }, Variants = new List<string> { "В каком году не стало рэпера Паши Техника?", "Викторина: назови год смерти Паши Техника." }},
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
                if (_config == null) throw new System.Exception("null config");
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
            permission.RegisterPermission(PermAdmin, this);
        }

        private void OnServerInitialized()
        {
            timer.Every(_config.IntervalSeconds, BroadcastQuestion);
        }

        [ChatCommand("trivia")]
        private void CmdTrivia(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermAdmin))
            {
                SendReply(player, "У тебя нет прав на это.");
                return;
            }

            BroadcastQuestion();
        }

        private void BroadcastQuestion()
        {
            if (_config.Questions == null || _config.Questions.Count == 0) return;

            var index = Random.Range(0, _config.Questions.Count);
            if (_config.Questions.Count > 1)
            {
                while (index == _lastIndex)
                    index = Random.Range(0, _config.Questions.Count);
            }
            _lastIndex = index;

            var question = _config.Questions[index];
            var variant = question.Variants[Random.Range(0, question.Variants.Count)];

            PrintToChat($"{_config.Prefix} {variant}");

            if (question.Answer != null && question.Answer.Count > 0)
                StartRound(question);
        }

        #region Answer round

        private void StartRound(Question question)
        {
            _round = new Round { Answer = question.Answer, Difficulty = question.Difficulty };
            timer.Once(_config.AnswerWindowSeconds, ResolveRound);
        }

        private double DifficultyMultiplier(string difficulty)
        {
            if (difficulty == "Лёгкий") return _config.EasyMultiplier;
            if (difficulty == "Сложный") return _config.HardMultiplier;
            return _config.MediumMultiplier;
        }

        private object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (_round == null || channel != Chat.ChatChannel.Global) return null;
            if (_round.Winners.Contains(player.userID)) return null;

            _round.Attempts.TryGetValue(player.userID, out var attempts);
            if (attempts >= _config.MaxAttempts) return null;

            _round.Attempts[player.userID] = attempts + 1;

            if (!IsCorrectAnswer(message)) return null;

            _round.Winners.Add(player.userID);

            var fake = _config.NonsenseReplies[Random.Range(0, _config.NonsenseReplies.Count)];
            PrintToChat($"{player.displayName}: {fake}");
            return true;
        }

        private bool IsCorrectAnswer(string message)
        {
            var lower = message.ToLowerInvariant();
            foreach (var keyword in _round.Answer)
            {
                if (lower.Contains(keyword.ToLowerInvariant()))
                    return true;
            }
            return false;
        }

        private void ResolveRound()
        {
            if (_round == null) return;

            var round = _round;
            _round = null;

            if (round.Winners.Count == 0)
            {
                PrintToChat($"Никто не угадал за {_config.AnswerWindowSeconds:0} сек. Правильный ответ: {round.Answer[0]}.");
                return;
            }

            var baseAmount = (long)System.Math.Round(_config.RewardPerWinner * DifficultyMultiplier(round.Difficulty));

            var parts = new List<string>();
            foreach (var playerId in round.Winners)
            {
                var player = BasePlayer.FindByID(playerId);
                var name = player != null ? player.displayName : playerId.ToString();

                long reward = 0;
                if (baseAmount > 0 && SatoshiCoins != null)
                {
                    var result = SatoshiCoins.Call("DepositScaled", playerId, baseAmount);
                    reward = result != null ? System.Convert.ToInt64(result) : 0L;
                }

                parts.Add($"{name} (+{reward})");
            }

            PrintToChat($"Правильный ответ: {round.Answer[0]}. Угадали: {string.Join(", ", parts)}.");
        }

        #endregion
    }
}
