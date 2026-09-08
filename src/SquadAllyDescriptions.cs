using System;
using System.Collections.Generic;
using HarmonyLib;
using MGSC;
using TMPro;

namespace Squad_Leader
{
    /// <summary>
    /// Third-person squad flavor for Magnum clone allies on RMB inspect.
    /// Does not touch Localization db - enemy / pact copies keep vanilla shadow text.
    /// </summary>
    internal static class SquadAllyDescriptions
    {
        private sealed class Loc
        {
            public string En;
            public string Ru;
            public string De;
            public string Fr;
            public string Es;
            public string Pl;
            public string Tr;
            public string Pt;
            public string Ko;
            public string Ja;
            public string Zh;

            public string Pick(Localization.Lang lang)
            {
                string s;
                switch (lang)
                {
                    case Localization.Lang.Russian: s = Ru; break;
                    case Localization.Lang.German: s = De; break;
                    case Localization.Lang.French: s = Fr; break;
                    case Localization.Lang.Spanish: s = Es; break;
                    case Localization.Lang.Polish: s = Pl; break;
                    case Localization.Lang.Turkish: s = Tr; break;
                    case Localization.Lang.BrazilianPortugal: s = Pt; break;
                    case Localization.Lang.Korean: s = Ko; break;
                    case Localization.Lang.Japanese: s = Ja; break;
                    case Localization.Lang.ChineseSimp: s = Zh; break;
                    case Localization.Lang.EnglishUS:
                    default: s = En; break;
                }
                if (!string.IsNullOrEmpty(s))
                    return s;
                return !string.IsNullOrEmpty(En) ? En : Ru;
            }
        }

        private static readonly Dictionary<string, Loc> ByProfileId =
            new Dictionary<string, Loc>(StringComparer.OrdinalIgnoreCase)
            {
                ["auberon_lukas"] = new Loc
                {
                    Ru = "Солидный оперативник ЧВК «Магнум» без лишних понтов. Ничем особо не выдается, но в перестрелке именно эта «рабочая лошадка» чаще всего удерживает отряд на ногах.",
                    En = "A solid Magnum PMC operative with no pretensions. Nothing flashy - but in a firefight this workhorse is the one who usually keeps the squad on its feet.",
                    De = "Ein solider Magnum-PMC-Operator ohne Alluren. Nichts Spektakulares - aber im Feuergefecht halt diese Arbeitspferd den Trupp meist am Leben.",
                    Fr = "Un operateur solide du PMC Magnum, sans fanfaronnade. Rien de spectaculaire - mais au feu, c'est ce cheval de trait qui tient souvent l'escouade debout.",
                    Es = "Un operativo solido del PMC Magnum, sin postureo. Nada espectacular - pero en un tiroteo este caballo de batalla suele mantener al escuadron en pie.",
                    Pl = "Solidny operatyw PMC Magnum bez poz. Nic widowiskowego - ale w wymianie ognia to wlasnie ten roboczy kon najczesciej trzyma oddzial na nogach.",
                    Tr = "Gosterissiz, saglam bir Magnum PMC operativi. Gosteris yok - ama catismada bu is atı genellikle mangayi ayakta tutar.",
                    Pt = "Um operativo solido do PMC Magnum, sem pose. Nada espetacular - mas no tiroteio e esse cavalo de batalha que costuma manter o esquadrao de pe.",
                    Ko = "허세 없는 단단한 마그넘 PMC 작전요원. 화려하진 않지만, 교전에서는 이 «일꾼»이 분대를 버티게 하는 경우가 많다.",
                    Ja = "見栄を張らない堅実なマグナムPMCオペレーティブ。派手さはないが、銃撃戦ではこの「働き馬」が分隊を支え続けることが多い。",
                    Zh = "务实低调的马格南PMC干员。并不耀眼 - 但交火时往往是这匹「干活的马」把小队撑住。",
                },
                ["bob_denarre"] = new Loc
                {
                    Ru = "Живой, громкий и опасно харизматичный. Рассуждает о мире так, будто спор можно решить только очередью. Бывает сложно определиться, избегать его или вместе отправиться в пекло.",
                    En = "Loud, vivid, and dangerously charismatic. Talks about peace as if the argument can only be settled with a full auto burst. Hard to decide whether to avoid him - or follow him into hell.",
                    De = "Laut, lebendig und gefahrlich charismatisch. Spricht vom Frieden, als liesse sich jeder Streit nur mit einer Feuerstoss-Serie klaren. Schwer zu sagen, ob man ihn meiden - oder mit ihm in die Holle gehen sollte.",
                    Fr = "Bruyant, vivant, dangereusement charismatique. Parle de paix comme si chaque desaccord se reglait a la rafale. Difficile de choisir: l'eviter - ou le suivre en enfer.",
                    Es = "Ruidoso, vivo y peligrosamente carismatico. Habla de paz como si cada disputa solo se resolviera a rafagas. Cuesta decidir si evitarlo - o seguirlo al infierno.",
                    Pl = "Glosny, zywotny i niebezpiecznie charyzmatyczny. Mowi o pokoju, jakby spor dalo sie rozstrzygnac tylko seria. Trudno zdecydowac: unikac go - czy isc z nim do piekla.",
                    Tr = "Gurultulu, canli ve tehlikeli derecede karizmatik. Barisi, tartismanin ancak seri atisle cozulebilecegi gibi anlatir. Ondan uzak durmak mi, yoksa onunla cehenneme mi gitmek - karar vermek zor.",
                    Pt = "Barulhento, vivo e perigosamente carismatico. Fala de paz como se toda briga so se resolvesse com rajada. Dificil decidir: evita-lo - ou segui-lo ao inferno.",
                    Ko = "시끄럽고 생생하며 위험할 만큼 카리스마 있다. 평화를 말하지만, 논쟁은 연사로만 끝나는 말투다. 피할 것인지, 함께 지옥으로 갈 것인지 결정이 어렵다.",
                    Ja = "騒がしく、生き生きして、危険なほどカリスマ的。平和の話を、論争はフルオートでしか終わらないかのように語る。避けるべきか、地獄まで付いていくべきか判断がつかない。",
                    Zh = "吵闹、鲜活、危险地有魅力。谈和平时像是争端只能用连发解决。很难说该躲着他 - 还是跟着他下地狱。",
                },
                ["edward_lowrance"] = new Loc
                {
                    Ru = "Обладает стандартной внешностью для наемника, однако не упускает возможности пару минут постоять у зеркала. Другие клоны закатывают глаза, когда он полчаса укладывает волосы перед каждой миссией, но это не мешает ему сохранить концентрацию в критический момент.",
                    En = "Looks like a standard mercenary, yet never skips a few minutes at the mirror. Other clones roll their eyes when he spends half an hour on his hair before every mission - but it never costs him focus when it counts.",
                    De = "Sieht aus wie ein Standardsolner, lasst sich aber nie die Minuten vor dem Spiegel nehmen. Andere Klone rollen mit den Augen, wenn er vor jeder Mission eine halbe Stunde an den Haaren arbeitet - die Konzentration im Ernstfall leidet darunter nicht.",
                    Fr = "Apparence de mercenaire standard, mais il ne rate jamais quelques minutes devant le miroir. Les autres clones lèvent les yeux au ciel quand il passe une demi-heure sur ses cheveux avant chaque mission - sa concentration au moment critique n'en souffre pas.",
                    Es = "Aspecto de mercenario estandar, pero nunca pierde unos minutos frente al espejo. Los demas clones ponen los ojos en blanco cuando dedica media hora al pelo antes de cada mision - sin perder concentracion en el momento critico.",
                    Pl = "Wyglada jak standardowy najemnik, ale nigdy nie odpusci kilku minut przed lustrem. Inne klony przewracaja oczami, gdy przed kazda misja pol godziny uklada wlosy - koncentracji w krytycznym momencie mu to nie zabiera.",
                    Tr = "Standart bir parali asker gorunumu var ama aynada birkac dakika gecirmeyi asla atlamaz. Her gorevden once sacina yarim saat harcayinca diger klonlar gozlerini devirir - kritik anda odaklanmasini bozmaz.",
                    Pt = "Parece um mercenario padrao, mas nunca abre mao de uns minutos no espelho. Os outros clones reviram os olhos quando ele gasta meia hora no cabelo antes de cada missao - sem perder o foco no momento critico.",
                    Ko = "평범한 용병 외모지만 거울 앞 몇 분은 빼먹지 않는다. 임무 전마다 머리에 삼십 분을 쓰면 다른 클론들은 눈을 흘기지만, 위기 순간 집중력은 잃지 않는다.",
                    Ja = "標準的な傭兵の外見だが、鏡の前の数分は欠かさない。任務前に毎回三十分も髪を整えると他のクローンは呆れるが、正念場の集中力は欠けない。",
                    Zh = "长相像标准雇佣兵，却从不放过镜子前的几分钟。每次任务前花半小时梳头时，其他克隆翻白眼 - 但关键时刻他照样集中。",
                },
                ["francis_reid_daly"] = new Loc
                {
                    Ru = "Любимчик Джейн, если верить слухам. Напарники уважают его за уверенный, вдохновляющий характер. Он не зайдет в комнату, если можно в нее ворваться.",
                    En = "Jane's favorite, if the rumors are true. Teammates respect his confident, inspiring presence. He will not walk into a room if he can kick the door in.",
                    De = "Janes Liebling, wenn man den Geruchten glaubt. Kameraden schatzen seine selbstsichere, mitreissende Art. Er betritt keinen Raum, wenn er die Tur eintreten kann.",
                    Fr = "Le chouchou de Jane, a en croire les rumeurs. Ses coequipiers respectent son assurance inspirante. Il n'entre pas dans une piece s'il peut enfoncer la porte.",
                    Es = "El favorito de Jane, si crees los rumores. Los companeros respetan su caracter seguro e inspirador. No entra en una habitacion si puede derribar la puerta.",
                    Pl = "Ulubieniec Jane, jesli wierzyc plotkom. Partnerzy cenia jego pewnosc siebie i charyzme. Nie wejdzie do pokoju, jesli moze wywazyc drzwi.",
                    Tr = "Dedikodulara inanilirsa Jane'in gozdesi. Takim arkadaslari guven verici, ilham dolu tavrina saygi duyar. Kapıyı tekmeleyebiliyorsa odaya yuruyerek girmez.",
                    Pt = "O favorito da Jane, se as fofocas forem verdade. Os companheiros respeitam a presenca segura e inspiradora. Ele nao entra numa sala se puder arrombar a porta.",
                    Ko = "소문이 맞다면 제인의 최애. 동료들은 자신감 있고 고무적인 태도를 존중한다. 문을 발로 찰 수 있으면 방으로 «걸어» 들어가지 않는다.",
                    Ja = "噂が本当ならジェーンのお気に入り。仲間はその自信に満ちた佇まいに敬意を払う。蹴破れるなら、部屋に歩いて入ることはない。",
                    Zh = "若谣言属实，他是简的宠儿。队友敬重他自信、鼓舞人的气场。能踹门就不会安安静静走进房间。",
                },
                ["hannah_reich"] = new Loc
                {
                    Ru = "Ее сделали инструментом, и она ответила тем, что стала самым острым в арсенале «Магнума». Рядом с ней клонам немного не по себе, хотя все знают, что на нее всегда можно положиться.",
                    En = "They made her a tool - and she answered by becoming the sharpest edge in Magnum's arsenal. Clones feel uneasy beside her, even though everyone knows she can be trusted.",
                    De = "Man machte sie zum Werkzeug - und sie antwortete, indem sie zur scharfsten Klinge im Arsenal von Magnum wurde. Neben ihr wird Klone unwohl, obwohl jeder weiss, dass man sich auf sie verlassen kann.",
                    Fr = "On en a fait un outil - et elle a repondu en devenant le tranchant le plus aigu de l'arsenal Magnum. Les clones sont mal a l'aise a ses cotes, meme s'ils savent qu'on peut compter sur elle.",
                    Es = "La convirtieron en herramienta - y respondio volviendose el filo mas afilado del arsenal de Magnum. Los clones se sienten incomodos a su lado, aunque todos saben que se puede confiar en ella.",
                    Pl = "Zrobiono z niej narzedzie - a ona odpowiedziala, stajac sie najostrzejszym ostrzem w arsenale Magnum. Klonon obok niej robi sie nieswojo, choc wszyscy wiedza, ze mozna na nia liczyc.",
                    Tr = "Onu bir araca cevirdiler - o da Magnum cephaneligindeki en keskin kenar olarak yanit verdi. Yaninda klonlar rahatsiz olur; yine de herkes ona guvenilebilecegini bilir.",
                    Pt = "Fizeram dela uma ferramenta - e ela respondeu tornando-se o fio mais afiado do arsenal Magnum. Os clones ficam desconfortaveis ao lado dela, embora todos saibam que da para confiar.",
                    Ko = "도구로 만들어졌고, 마그넘 무기고에서 가장 날카로운 날이 되는 것으로 응수했다. 곁에 있으면 불편하지만, 믿을 수 있다는 건 모두가 안다.",
                    Ja = "道具にされた彼女は、マグナムの兵器庫で最も鋭い刃になることで応えた。傍にいると居心地が悪いが、頼りになることは皆知っている。",
                    Zh = "他们把她做成工具 - 她则以成为马格南军火库里最锋利的刃作为回答。身边的克隆会不自在，但人人都知道她靠得住。",
                },
                ["Isabella_capet"] = new Loc
                {
                    Ru = "Прозвище «Волчица» она получила не зря: благородные черты и ни грамма мягкости. В каждом изгибе лица читается - она профессионал. Свободное время предпочитает проводить в одиночестве, поэтому другим клонам бывает непросто найти с ней общий язык.",
                    En = "The callsign She-Wolf fits: noble features and not a gram of softness. Every line of her face says professional. She prefers solitude off-duty, so other clones often struggle to connect with her.",
                    De = "Der Rufname Wolfin passt: edle Zuge und kein Gramm Weichheit. Jede Linie im Gesicht sagt Profi. Freizeit verbringt sie lieber allein - anderen Klonen fallt der Draht zu ihr oft schwer.",
                    Fr = "Le surnom Louve n'est pas vole: traits nobles, aucune douceur. Chaque ligne du visage dit professionnelle. Elle prefere la solitude hors service - les autres clones peinent souvent a trouver le contact.",
                    Es = "El indicativo Loba le viene al pelo: rasgos nobles y ni un gramo de blandura. Cada linea de su rostro dice profesional. Prefiere la soledad fuera de servicio - a otros clones les cuesta conectar.",
                    Pl = "Pseudonim Wilczyca nie bez powodu: szlachetnne rysy i ani grama miekkosci. Kazda linia twarzy mowi: profesjonalistka. Po sluzbie woli samotnosc - innym klonom trudno nawiazac z nia kontakt.",
                    Tr = "Disi Kurt cagrisi bosuna degil: asil hatlar, zerrece yumusaklik yok. Yuzunun her cizgisi profesyonel der. Gorev disinda yalnizligi tercih eder - diger klonlarin bag kurmasi zorlasir.",
                    Pt = "O indicativo Loba nao e a toa: tracos nobres e zero moleza. Cada linha do rosto diz profissional. Prefere solidão fora de servico - outros clones tem dificuldade de se aproximar.",
                    Ko = "암늑대라는 콜사인이 어울린다: 고귀한 이목구비에 부드러움은 없다. 얼굴 선마다 프로가 읽힌다. 비번엔 혼자를 선호해 다른 클론이 친해지기 어렵다.",
                    Ja = "シーウルフのコールサインは伊達ではない。気品ある顔立ちに柔らかさはゼロ。表情のすべてがプロを語る。非番は孤独を好み、他のクローンは距離を縮めにくい。",
                    Zh = "母狼的呼号名副其实：高贵五官，没有一丝软。脸部线条处处写着专业。休班更爱独处，其他克隆很难跟她投缘。",
                },
                ["jacques_kennet"] = new Loc
                {
                    Ru = "Клон стрелка, собранный стрелками для стрельбы. Биоинженеры «Магнума» так увлеклись его огневой специализацией, что ковбой уже не кажется человеком. Он всегда старается найти компанию других клонов, чтобы о чем-нибудь поболтать, но ему бывает непросто из-за того, что он не может говорить ни о чем, кроме стрельбы.",
                    En = "A shooter clone built by shooters for shooting. Magnum bioengineers leaned so hard into his gun specialty that Cowboy barely feels human. He always tries to chat with other clones - but can talk about nothing except shooting.",
                    De = "Ein Schutzenklon, von Schutzen furs Schiessen gebaut. Magnums Bioingenieure haben die Feuerwaffen-Spezialisierung so weit getrieben, dass Cowboy kaum noch menschlich wirkt. Er sucht Gesprach - und kann uber nichts ausser Schiessen reden.",
                    Fr = "Un clone tireur, assemble par des tireurs pour tirer. Les bioingenieurs Magnum ont pousse sa specialite feu si loin que Cowboy parait a peine humain. Il cherche toujours a discuter - mais ne parle que de tir.",
                    Es = "Un clon tirador, hecho por tiradores para tirar. Los bioingenieros de Magnum llevaron su especialidad de fuego tan lejos que Cowboy apenas parece humano. Siempre busca charla - y solo puede hablar de disparar.",
                    Pl = "Klon strzelca, zbudowany przez strzelcow do strzelania. Bioinzynierowie Magnum tak pchneli specjalizacje ogniowa, ze Kowboj ledwie wydaje sie czlowiekiem. Chce gadac z innymi - ale umie tylko o strzelaniu.",
                    Tr = "Nisancilarin nisancilik icin yaptigi bir nisanci klonu. Magnum biyo-muhendisleri ates uzmanligini o kadar abartti ki Kovboy zar zor insan gibi duruyor. Sohbet arar - ama atistan baska konu acamaz.",
                    Pt = "Um clone atirador, feito por atiradores para atirar. Os bioengenheiros da Magnum empurraram a especialidade de fogo tao longe que o Cowboy mal parece humano. Sempre tenta conversar - e so fala de tiro.",
                    Ko = "사격수용으로 사격수가 만든 클론. 마그넘 생체공학자들이 화력 특화를 밀어붙여 카우보이는 사람 같아 보이지 않는다. 수다를 원하지만 사격 말고는 말이 없다.",
                    Ja = "射手が射撃のために組んだ射手クローン。マグナムの生体工学が銃火器特化を押し進めすぎて、カウボーイは人らしく見えない。雑談したいのに、射撃以外の話題が出ない。",
                    Zh = "射手为射击组装的射手克隆。马格南生物工程师把他的火力专精推得太远，牛仔几乎不像人。他总想找人闲聊 - 却只会聊射击。",
                },
                ["jan_shrammert"] = new Loc
                {
                    Ru = "Поджарый, усталый и смертоносный оперативник. Гнется как проволока, и иногда Джейн приходится пару часов искать его на «Магнуме» перед каждой операцией. Напарники всегда знают: если Джека не видно, значит в самый ответственный момент он будет готов нанести врагу удар в спину.",
                    En = "Lean, tired, and lethal. Bends like wire - and Jane sometimes spends hours hunting him down on Magnum before a mission. Teammates know: if Jack is missing, he will be ready to stab the enemy in the back when it matters.",
                    De = "Mager, mude und todlich. Biegt sich wie Draht - und Jane sucht ihn vor Einsatzen manchmal stundenlang auf Magnum. Kameraden wissen: fehlt Jack, steht er im entscheidenden Moment bereit, dem Feind in den Rucken zu fallen.",
                    Fr = "Sec, fatigue, mortel. Souple comme un fil - et Jane passe parfois des heures a le retrouver sur Magnum avant une mission. Les coequipiers savent: si Jack disparait, il sera la pour poignarder l'ennemi dans le dos au bon moment.",
                    Es = "Flaco, cansado y letal. Se dobla como alambre - y a veces Jane tarda horas en encontrarlo en Magnum antes de una mision. Los companeros saben: si Jack no aparece, estara listo para apunalar al enemigo por la espalda cuando importe.",
                    Pl = "Chudy, zmeczony i smiertelny. Gie sie jak drut - a Jane czasem godzinami szuka go na Magnum przed misja. Partnerzy wiedza: jesli Jacka nie widac, w kluczowym momencie wbijе noz wrogowi w plecy.",
                    Tr = "Siska, yorgun ve oldurucu. Tel gibi egilir - Jane bazen gorev oncesi Magnum'da onu saatlerce arar. Arkadaslari bilir: Jack ortalikta yoksa, kritik anda dusmanin sirtina bıcak saplamaya hazirdir.",
                    Pt = "Magro, cansado e letal. Dobra como arame - e Jane as vezes gasta horas caçando-o na Magnum antes da missao. Os companheiros sabem: se Jack sumiu, estara pronto para esfaquear o inimigo pelas costas na hora certa.",
                    Ko = "마르고 피곤하며 치명적이다. 철사처럼 휘고, 제인는 임무 전 마그넘에서 그를 몇 시간 찾기도 한다. 동료들은 안다: 잭이 안 보이면, 결정적 순간에 적의 등을 칠 준비가이다.",
                    Ja = "痩せ、疲れ、致死的。針金のようにしなり、ジェーンは任務前にマグナムで何時間も探すことがある。仲間は知っている。ジャックが見えないなら、正念場で敵の背中を突く準備ができている。",
                    Zh = "精瘦、疲惫、致命。像铁丝一样弯折 - 简有时任务前要在马格南上找他几个小时。队友都清楚：看不见杰克，就意味着关键时刻他会捅敌人的背。",
                },
                ["john_hawkwood_boss"] = new Loc
                {
                    Ru = "Засекреченный актив «Магнума». Досье обрывается на позывном. Чем бы ни был Босс, другие клоны воспринимают тишину вокруг него как часть снаряжения.",
                    En = "A classified Magnum asset. The file stops at the callsign. Whatever Boss is, other clones treat the silence around him as part of the kit.",
                    De = "Ein geheimes Magnum-Asset. Die Akte endet beim Rufnamen. Was Boss auch ist - andere Klone behandeln die Stille um ihn wie Teil der Ausrustung.",
                    Fr = "Un actif classe Magnum. Le dossier s'arrete au indicatif. Quoi que soit Boss, les autres clones traitent le silence autour de lui comme une piece d'equipement.",
                    Es = "Un activo clasificado de Magnum. El expediente se corta en el indicativo. Sea lo que sea Boss, los demas clones tratan el silencio a su alrededor como parte del equipo.",
                    Pl = "Utajniony aktyw Magnum. Teczka urywa sie na pseudonimie. Czymkolwiek jest Boss, inne klony traktuja cisze wokol niego jak czesc ekwipunku.",
                    Tr = "Gizli bir Magnum varligi. Dosya cagri isaretinde kesilir. Boss ne olursa olsun, diger klonlar etrafındaki sessizligi teçhizatın bir parçası sayar.",
                    Pt = "Um ativo classificado da Magnum. O dossie para no indicativo. Seja la o que Boss for, os outros clones tratam o silencio ao redor dele como parte do kit.",
                    Ko = "기밀 마그넘 자산. 서류는 콜사인에서 끊긴다. 보스가 무엇이든, 다른 클론들은 주변 침묵을 장비의 일부로 여긴다.",
                    Ja = "機密のマグナム資産。ファイルはコールサインで途切れる。ボスが何であれ、他のクローンは周囲の沈黙を装備の一部として扱う。",
                    Zh = "马格南的机密资产。档案停在呼号。无论老大是什么，其他克隆把围绕他的沉默当成装备的一部分。",
                },
                ["kenzie_yukio"] = new Loc
                {
                    Ru = "Само его тело выглядит как смертельное оружие, готовое действовать раньше, чем кто-то успеет выхватить ствол, хотя его взгляд кажется доброжелательным независимо от обстановки вокруг.",
                    En = "His body itself looks like a lethal weapon, ready to move before anyone draws - yet his gaze stays kindly no matter the situation.",
                    De = "Sein Korper wirkt selbst wie eine todliche Waffe, bereit zu handeln, bevor jemand zieht - und doch bleibt der Blick freundlich, egal wie die Lage ist.",
                    Fr = "Son corps meme semble une arme mortelle, pret a agir avant que quiconque degaine - son regard reste bienveillant, quelle que soit la situation.",
                    Es = "Su propio cuerpo parece un arma letal, lista para actuar antes de que alguien desenfund - y su mirada sigue amable sea cual sea la situacion.",
                    Pl = "Jego cialo wyglada jak smiertelna bron, gotowa dzialac zanim ktos dobyje - a wzrok pozostaje zyczliwy niezaleznie od sytuacji.",
                    Tr = "Vucudu tek basina oldurucu bir silah gibi; biri silah cekmeden harekete hazir - bakisi yine de ortama bakmaksizin iyimser kalir.",
                    Pt = "O proprio corpo parece uma arma letal, pronto para agir antes que alguem sacque - e o olhar continua gentil, seja qual for a situacao.",
                    Ko = "몸 자체가 치명 무기처럼 보여, 누가 총을 뽑기 전에 움직일 태세다. 그럼에도 시선은 상황과 상관없이 온화하다.",
                    Ja = "その体自体が致死兵器に見え、誰かが銃を抜く前に動く準備ができている。それでも視線は状況に関係なく温和だ。",
                    Zh = "身体本身就像致命武器，准备在任何人拔枪前动手 - 目光却无论局势如何都显得和善。",
                },
                ["laksha_saminath"] = new Loc
                {
                    Ru = "Тихая компетентность с тенью сомнения, которую он не произносит вслух. Работая с администрацией «Магнума», Лакша демонстрирует крайне высокую концентрацию и внимательность. Благодаря ему и Джейн корпорациям почти никогда не удается поймать ЧВК на крючок, подсунув какой-нибудь дырявый контракт.",
                    En = "Quiet competence with a shadow of doubt he never voices. Working Magnum admin, Laksha brings razor focus. Thanks to him and Jane, corporations almost never hook the PMC with a leaky contract.",
                    De = "Stille Kompetenz mit einem Schatten von Zweifel, den er nie ausspricht. In der Magnum-Verwaltung arbeitet Laksha hochkonzentriert. Dank ihm und Jane bekommen Konzerne die PMC fast nie mit einem lochrigen Vertrag zu fassen.",
                    Fr = "Competence tranquille, avec une ombre de doute qu'il ne dit jamais. Aupres de l'admin Magnum, Laksha est d'une concentration extreme. Grace a lui et a Jane, les corporations n'attrapent presque jamais le PMC avec un contrat penche.",
                    Es = "Competencia callada, con una sombra de duda que no dice. En la admin de Magnum, Laksha aporta concentracion extrema. Gracias a el y a Jane, las corporaciones casi nunca enganchan al PMC con un contrato agujereado.",
                    Pl = "Cicha kompetencja z cienem watpliwosci, ktorej nie wypowiada. Przy administracji Magnum Laksha trzyma ostra koncentracje. Dzieki niemu i Jane korporacje prawie nigdy nie lapia PMC na dziurawy kontrakt.",
                    Tr = "Sesiz yeterlilik; dile getirilmeyen bir suphe golgesi. Magnum yonetiiminde Laksha yogun odaklanir. Onun ve Jane sayesinde sirketler PMC'yi delik bir sozlesmeyle neredeyse hic yakalayamaz.",
                    Pt = "Competencia quieta, com uma sombra de duvida que nao verbaliza. Na admin da Magnum, Laksha traz foco extremo. Por ele e pela Jane, corporacoes quase nunca fisgam o PMC com contrato furado.",
                    Ko = "말없는 유능함, 입 밖으로 내지 않는 의심의 그림자. 마그넘 행정에서 락샤는 극도의 집중을 보인다. 그와 제인 덕에 기업이 허술한 계약으로 PMC를 낚는 일은 거의 없다.",
                    Ja = "静かな有能さ。口に出さない疑念の影がある。マグナム事務ではラクシャの集中は鋭い。彼とジェーンのおかげで、企業は穴だらけの契約でPMCをほとんど釣れない。",
                    Zh = "安静的能干，带着从不说出口的疑影。在马格南行政侧，拉克沙极度专注。多亏他和简，公司几乎没法用破合同钓到这家PMC。",
                },
                ["marika_wulfnod"] = new Loc
                {
                    Ru = "Одна из самых ярких оперативниц «Магнума». Другие клоны высоко ценят ее не только за красоту, но и за эффективность в бою. Хотя при личном разговоре она бывает такой замкнутой, что кажется, будто даже просто поздороваться для нее великий труд.",
                    En = "One of Magnum's most striking operatives. Other clones value her for beauty and combat efficiency alike. One-on-one she can be so withdrawn that even a hello feels like hard labor.",
                    De = "Eine der auffalligsten Operatorinnen Magnums. Andere Klone schatzen Schonheit und Kampfeffizienz gleichermassen. Im Gesprach kann sie so zuruckgezogen sein, dass selbst ein Hallo wie Schwerstarbeit wirkt.",
                    Fr = "L'une des operatrices les plus marquantes de Magnum. Les autres clones apprecient beaute et efficacite au combat. En tete-a-tete elle peut etre si fermee qu'un simple bonjour semble un labeur.",
                    Es = "Una de las operativas mas llamativas de Magnum. Otros clones valoran belleza y eficacia en combate. En trato personal puede estar tan cerrada que hasta un hola parece un esfuerzo enorme.",
                    Pl = "Jedna z najbardziej wyrazistych operatywek Magnum. Inne klony cenia urode i skutecznosc w boju. W rozmowie bywa tak zamknieta, ze nawet czesc sprawia wrazenie ciezkiej pracy.",
                    Tr = "Magnum'un en dikkat ceken operatiflerinden. Diger klonlar onu guzelligi ve savas verimi icin over. Bire bir konusmada o kadar icinedonuktur ki merhaba bile buyuk efor gibi gelir.",
                    Pt = "Uma das operativas mais marcantes da Magnum. Outros clones valorizam beleza e eficiencia em combate. Em conversa pessoal pode ser tao fechada que ate um oi parece trabalho pesado.",
                    Ko = "마그넘에서 가장 눈에 띄는 작전요원 중 하나. 다른 클론들은 미모와 전투 효율을 함께 친다. 개인 대화에서는 인사조차 중노동처럼 느껴질 만큼 닫혀 있을 수 있다.",
                    Ja = "マグナムで最も目立つオペレーティブの一人。他のクローンは美貌と戦闘効率の両方を評価する。個人的な会話では、挨拶さえ重労働に見えるほど閉じることがある。",
                    Zh = "马格南最醒目的干员之一。其他克隆既看重外表，也看重战场效率。私下交谈时她可能封闭到连打招呼都像苦差。",
                },
                ["maximilian_rohr"] = new Loc
                {
                    Ru = "Выглядит именно так, как должен выглядеть клон, способный выйти оттуда, где остальные остаются лежать. Макс не хвастается - за него говорит статистика выживания.",
                    En = "Looks exactly like a clone who can walk out where others stay down. Max does not brag - survival stats speak for him.",
                    De = "Sieht genau so aus, wie ein Klon aussehen muss, der dort rausgeht, wo andere liegen bleiben. Max prahlt nicht - die Uberlebensstatistik spricht fur ihn.",
                    Fr = "A exactement l'air d'un clone capable de ressortir la ou les autres restent a terre. Max ne se vante pas - les stats de survie parlent pour lui.",
                    Es = "Parece exactamente el clon que puede salir de donde otros se quedan tirados. Max no alardea - las estadisticas de supervivencia hablan por el.",
                    Pl = "Wyglada dokladnie jak klon, ktory wyjdzie stad, gdzie inni zostaja lezec. Max sie nie chwali - statystyki przezycia mowia za niego.",
                    Tr = "Baskalarinin yatip kaldigi yerden cikabilen bir klonun tam goruntusu. Max ovunmez - hayatta kalma istatistikleri onun icin konusur.",
                    Pt = "Parece exatamente o clone capaz de sair de onde os outros ficam no chao. Max nao se gaba - a estatistica de sobrevivencia fala por ele.",
                    Ko = "남들이 쓰러져 남는 곳에서 걸어 나올 클론의 모습 그대로다. 맥스는 자랑하지 않는다 - 생존 통계가 대신 말한다.",
                    Ja = "他が倒れたままになる場所から出てこられるクローンそのものの見た目だ。マックスは自慢しない。生存率が代わりに語る。",
                    Zh = "看起来就像能从别人倒地的地方走出来的克隆。马克斯不吹牛 - 生存数据替他说。",
                },
                ["mirza_aishatu"] = new Loc
                {
                    Ru = "Коренастая женщина, известная генетическими модификациями пищеварительной системы. В столовую она приходит последней, доедает за другими клонами и «утилизирует» пищевые остатки с кухни. Другие клоны всегда рады ее компании, хотя иногда их смущает то, что она не стесняется жевать объедки, пока болтает.",
                    En = "A stocky woman known for digestive-system gene mods. She arrives last at the mess, finishes others' plates, and «recycles» kitchen scraps. Clones enjoy her company - though it unsettles them that she unapologetically chews leftovers while chatting.",
                    De = "Eine stämmige Frau, bekannt fur Gen-Mods am Verdauungssystem. Kommt zuletzt in die Kantine, isst Teller anderer auf und «recycelt» Kuchenabfalle. Klone mogen ihre Gesellschaft - irritiert sie aber, dass sie beim Reden ungeniert Essensreste kaut.",
                    Fr = "Une femme trapue, connue pour des mods genetiques digestifs. Arrive derniere a la cantine, finit les assiettes des autres et « recycle » les restes. Les clones aiment sa compagnie - meme si ca gene qu'elle mastique des restes en bavardant sans gene.",
                    Es = "Una mujer fornida, conocida por mods geneticos digestivos. Llega ultima al comedor, termina platos ajenos y «recicla» restos de cocina. Los clones disfrutan su compania - aunque incomoda que mastique sobras mientras habla, sin verguenza.",
                    Pl = "Krępa kobieta znana z gen-modow ukladu trawiennego. Do stołowki wchodzi ostatnia, dojada za innymi i «utylizuje» resztki z kuchni. Klony ciesza sie jej towarzystwem - choc razi, ze bez wstydu zurze resztki w trakcie rozmowy.",
                    Tr = "Sindirim sistemi gen modlariyla bilinen bodur bir kadin. Yemekhaneye en son gelir, baskalarinin tabagini bitirir, mutfak artigini «geri donusturur». Klonlar sirketinden hoslanir - sohbet ederken artıkları utanmadan cignemesi bazen rahatsiz eder.",
                    Pt = "Mulher robusta, conhecida por mods geneticos digestivos. Chega por ultimo ao refeitorio, termina pratos alheios e «recicla» restos da cozinha. Os clones gostam da companhia - embora incomode que mastigue sobras sem pudor enquanto conversa.",
                    Ko = "소화계 유전자 개조로 유명한 다부진 여성. 식당에 맨 마지막에 와 남의 접시를 비우고 주방 찌꺼기를 «재활용»한다. 클론들은 그녀와 함께하는 걸 좋아하지만, 수다 중에 거리낌 없이 찌꺼기를 씹는 건 불편하다.",
                    Ja = "消化器系の遺伝子改変で知られるがっしりした女性。食堂には最後に現れ、他人の皿を片付け、厨房の残りを「再利用」する。クローンは彼女の同席を喜ぶが、話しながら残飯を平気で噛む姿には戸惑う。",
                    Zh = "以消化系统基因改造闻名的敦实女人。食堂总最后一个到，扫完别人的盘子，还「处理」厨房残羹。克隆们喜欢她作伴 - 却也别扭：她边聊边坦然嚼剩饭。",
                },
                ["niko_medich"] = new Loc
                {
                    Ru = "Эффективный и жестокий солдат. Результаты психологического тестирования клонов всегда анонимны, но другие клоны видят, как седые терапевты спешат отпроситься на перерыв, как только видят Плута в медицинском отделении «Магнума».",
                    En = "An efficient, brutal soldier. Clone psych evals stay anonymous - but other clones watch gray therapists scramble for a break the moment Rogue walks into Magnum medbay.",
                    De = "Ein effizienter, brutaler Soldat. Psychotests der Klone bleiben anonym - doch andere sehen, wie ergraute Therapeuten um Pause bitten, sobald Schurke in Magnums Medstation auftaucht.",
                    Fr = "Soldat efficace et brutal. Les evals psy des clones restent anonymes - mais les autres voient les therapeutes chenus demander une pause des que Fripouille entre a l'infirmerie Magnum.",
                    Es = "Soldado eficiente y brutal. Las pruebas psi de los clones son anonimas - pero otros ven a terapeutas canosos pedir descanso en cuanto Picaro entra en la enfermeria de Magnum.",
                    Pl = "Skuteczny i okrutny zolnierz. Testy psychologiczne klonow sa anonimowe - ale inne widza, jak siwi terapeuci proszą o przerwe, gdy tylko Lotr wchodzi do ambulatorium Magnum.",
                    Tr = "Verimli ve acimasiz bir asker. Klon psik testleri anonim kalir - ama Magnum medikaline Dolandırıcı girer girmez kırlaşmış terapistlerin mola istedigini digerleri gorur.",
                    Pt = "Soldado eficiente e brutal. Avaliacoes psi dos clones sao anonimas - mas outros veem terapeutas grisalhos pedindo intervalo assim que Trapaceiro entra na enfermaria Magnum.",
                    Ko = "효율적이고 잔인한 병사. 클론 심리 검사는 익명이지만, 마그넘 의무실에 로그가 들어오는 순간 백발 치료사들이 휴식을 청하는 걸 다른 클론들은 본다.",
                    Ja = "効率的で残酷な兵士。クローンの心理検査は匿名だが、マグナム医務室にローグが入った瞬間、白髪のセラピストが休憩を願うのを他のクローンは見ている。",
                    Zh = "高效而残酷的士兵。克隆心理测评匿名 - 但其他克隆看得见：浪子一进马格南医务室，灰发治疗师就急着请假休息。",
                },
                ["persival_fawcett"] = new Loc
                {
                    Ru = "Солдат поистине мифологической внешности. Паладин выглядит так, будто «Магнум» собрал не клона, а танк. От его голоса стекло начинает дрожать, а враги - разбегаться в стороны. Несмотря на такой внушительный внешний вид, друзья знают его как доброго наставника, подающего живой пример новобранцам.",
                    En = "A soldier of near-mythic build. Paladin looks like Magnum assembled a tank, not a clone. His voice rattles glass - enemies scatter. For all that presence, friends know a kind mentor who sets a living example for recruits.",
                    De = "Ein Soldat von geradezu mythischer Gestalt. Paladin wirkt, als habe Magnum einen Panzer statt eines Klons gebaut. Seine Stimme lasst Glas zittern - Feinde stieben. Trotz der Prasenz kennen Freunde einen guten Mentor, der Rekruten Vorbild ist.",
                    Fr = "Un soldat a l'allure presque mythique. Paladin semble un char assemble par Magnum, pas un clone. Sa voix fait trembler le verre - les ennemis se dispersent. Malgre la stature, les amis connaissent un mentor bienveillant, exemple vivant pour les recrues.",
                    Es = "Soldado de aspecto casi mitologico. Paladin parece que Magnum monto un tanque, no un clon. Su voz hace temblar el cristal - los enemigos se dispersan. Pese a la presencia, los amigos conocen a un mentor amable, ejemplo vivo para reclutas.",
                    Pl = "Zolnierz o niemal mitycznej budowie. Paladyn wyglada, jakby Magnum zlozylo czolg, nie klona. Jego glos drzy w szkle - wrogowie sie rozpraszaja. Mimo wizerunku przyjaciele znaja dobrego mentora, zywy przyklad dla rekrutow.",
                    Tr = "Neredeyse efsanevi gorunumlu bir asker. Paladin, Magnum'un klon degil tank monte ettigi gibi durur. Sesi camı titreştirir - dusmanlar dagilir. Bu heybetе ragmen dostlar onu, acemilere ornek olan iyi bir mentor olarak bilir.",
                    Pt = "Soldado de porte quase mitologico. Paladino parece que a Magnum montou um tanque, nao um clone. A voz faz o vidro tremer - inimigos se espalham. Apesar da presença, amigos conhecem um mentor gentil, exemplo vivo para recrutas.",
                    Ko = "거의 신화적 체구의 병사. 팔라딘은 마그넘이 클론이 아니라 전차를 조립한 듯하다. 목소리에 유리가 떨리고 적은 흩어진다. 그 위압에도 친구들은 신병에게 산 본보기가 되는 다정한 멘토로 안다.",
                    Ja = "ほぼ神話級の体躯の兵士。パラディンはマグナムがクローンではなく戦車を組んだように見える。声でガラスが震え、敵は散る。その威圧感にもかかわらず、友人は新兵の生きた手本となる優しい指導者と知る。",
                    Zh = "近乎神话体格的士兵。圣骑士看起来像马格南组装的不是克隆而是坦克。嗓音震得玻璃发颤 - 敌人四散。尽管气场压人，朋友们知道他是温厚导师，给新兵活榜样。",
                },
                ["priya_marlon"] = new Loc
                {
                    Ru = "Сильная, жестокая, эффективная - и всегда считает добычу. Аббатиса предпочитает победу с прибылью. Клоны «Магнума» быстро учатся не вставать между ней и наваром.",
                    En = "Strong, cruel, efficient - and always counting loot. Abbess prefers victory with a profit. Magnum clones learn fast not to stand between her and the take.",
                    De = "Stark, grausam, effizient - und zahlt stets die Beute. Abtissin bevorzugt Sieg mit Gewinn. Magnum-Klone lernen schnell, sich nicht zwischen sie und den Profit zu stellen.",
                    Fr = "Forte, cruelle, efficace - et compte toujours le butin. Abbesse prefere la victoire rentable. Les clones Magnum apprennent vite a ne pas se mettre entre elle et le gain.",
                    Es = "Fuerte, cruel, eficiente - y siempre cuenta el botin. Abadesa prefiere victoria con beneficio. Los clones de Magnum aprenden rapido a no interponerse entre ella y el botin.",
                    Pl = "Silna, okrutna, skuteczna - i zawsze liczy lup. Przelozona woli zwyciestwo z zyskiem. Klony Magnum szybko ucza sie nie stawac miedzy nia a zarobkiem.",
                    Tr = "Guclu, acimasiz, verimli - ve her zaman ganimeti sayar. Rahibe kazançli zaferi tercih eder. Magnum klonlari onunla pay arasina girmemeyi çabuk ogrenir.",
                    Pt = "Forte, cruel, eficiente - e sempre conta o saque. Abadessa prefere vitoria com lucro. Clones da Magnum aprendem rapido a nao ficar entre ela e o ganho.",
                    Ko = "강하고 잔인하며 효율적 - 전리품도 늘 센다. 압베스는 수익 나는 승리를 선호한다. 마그넘 클론들은 그녀와 몫 사이에 서지 않는 법을 빨리 배운다.",
                    Ja = "強く、残酷で、効率的 - しかも戦利品を数え続ける。アベスは利益付きの勝利を好む。マグナムのクローンは、彼女と取り分の間に立たないことを早く覚える。",
                    Zh = "强壮、残酷、高效 - 还总在清点战利品。女修道院院长更爱带利润的胜利。马格南克隆很快学会别挡在她和油水之间。",
                },
                ["victoria_boudicca"] = new Loc
                {
                    Ru = "Характерный сарказм Боудикки корежит даже самых хладнокровных. Другие клоны уважают ее за тактические навыки и боятся ее комментариев в свой адрес.",
                    En = "Boudicca's trademark sarcasm scrapes even the coldest nerves. Other clones respect her tactics - and fear her remarks aimed at them.",
                    De = "Boudiccas Markenzeichen-Sarkasmus kratzt selbst an den kaltesten Nerven. Andere Klone achten ihre Taktik - und furchten ihre Kommentare uber sie.",
                    Fr = "Le sarcasme signature de Boudicca agace meme les plus froids. Les autres clones respectent sa tactique - et craignent ses remarques a leur egard.",
                    Es = "El sarcasmo caracteristico de Boudicca roce hasta a los mas frios. Otros clones respetan su tactica - y temen sus comentarios hacia ellos.",
                    Pl = "Charakterystyczny sarkazm Boudikki drażni nawet najbardziej zimnokrwistych. Inne klony cenia jej taktyke - i boja sie jej uwag pod swoim adresem.",
                    Tr = "Boudicca'nin imza alayi en sogukkanlilari bile tirmalar. Diger klonlar taktigine saygi duyar - ve kendilerine yonelik yorumlarindan korkar.",
                    Pt = "O sarcasmo tipico de Boudicca arranha ate os mais frios. Outros clones respeitam a tatica - e temem os comentarios dela sobre eles.",
                    Ko = "보우디카 특유의 빈정거림은 가장 냉정한 이도 긁는다. 다른 클론들은 전술을 존중하고, 자신에 대한 코멘트는 두려워한다.",
                    Ja = "ブーディカ特有の皮肉は、最も冷静な者の神経も削る。他のクローンは戦術を尊敬し、自分宛ての発言を恐れる。",
                    Zh = "布狄卡标志性的挖苦能刮到最冷血的人。其他克隆敬重她的战术 - 又怕她冲自己开的点评。",
                },
            };

        public static string NormalizeProfileId(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return profileId;
            if (profileId.EndsWith("_custom", StringComparison.OrdinalIgnoreCase))
                return profileId.Substring(0, profileId.Length - "_custom".Length);
            return profileId;
        }

        private static Localization.Lang CurrentLang()
        {
            try
            {
                return Singleton<Localization>.Instance.CurrentLang;
            }
            catch
            {
                return Localization.Lang.EnglishUS;
            }
        }

        public static bool TryGetForSquadAlly(CreatureData data, out string description)
        {
            description = null;
            if (data == null || !Plugin.Config.Enabled || !SquadRaidState.Active)
                return false;

            if (!SquadRaidState.UniqueIdToProfileId.TryGetValue(data.UniqueId, out var profileId))
                return false;

            return TryGetByProfileId(profileId, out description);
        }

        public static bool TryGetByProfileId(string profileId, out string description)
        {
            description = null;
            if (string.IsNullOrEmpty(profileId))
                return false;

            Loc loc = null;
            if (!ByProfileId.TryGetValue(profileId, out loc))
                ByProfileId.TryGetValue(NormalizeProfileId(profileId), out loc);
            if (loc == null)
                return false;

            description = loc.Pick(CurrentLang());
            return !string.IsNullOrEmpty(description);
        }
    }

    [HarmonyPatch(typeof(MonsterInspectWindow), nameof(MonsterInspectWindow.Configure))]
    internal static class SquadAllyMonsterInspectDescPatch
    {
        private static void Postfix(MonsterInspectWindow __instance, Creature creature)
        {
            if (creature?.CreatureData == null)
                return;
            if (!(creature is Monster monster) || !AllyMovementService.IsSquadAlly(monster))
                return;
            if (!SquadAllyDescriptions.TryGetForSquadAlly(creature.CreatureData, out var desc))
                return;

            try
            {
                var field = Traverse.Create(__instance).Field<TextMeshProUGUI>("_corpseDesc");
                var label = field.Value;
                if (label == null)
                    return;
                label.text = desc;
                Localization.ActualizeFontAndSize(label, TextContext.LongText);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogException(ex);
            }
        }
    }

    [HarmonyPatch(typeof(CorpseInspectWindow), nameof(CorpseInspectWindow.Configure), typeof(CorpseStorage))]
    internal static class SquadAllyCorpseInspectDescPatch
    {
        private static void Postfix(CorpseInspectWindow __instance, CorpseStorage corpseStorage)
        {
            if (corpseStorage?.CreatureData == null)
                return;
            if (!SquadAllyDescriptions.TryGetForSquadAlly(corpseStorage.CreatureData, out var desc))
                return;

            try
            {
                var label = Traverse.Create(__instance).Field<TextMeshProUGUI>("_corpseDesc").Value;
                if (label == null)
                    return;
                label.text = desc;
                Localization.ActualizeFontAndSize(label, TextContext.LongText);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogException(ex);
            }
        }
    }
}
