using System.Collections.Generic;

namespace SWTORCombatParser.DataStructures.EncounterInfo;

public static class NewRaidFormat
{
    public static void AddIdsToEncounters(List<EncounterInfo> currentEncounters)
    {
        foreach (var encounter in currentEncounters)
        {
            switch (encounter.Name)
            {
                case "Eternity Vault":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Annihilation Droid XRR-3", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 1779997656219648L } },
                                { "Story 16", new List<long>() { 2017165750304768L } },
                                { "Veteran 8", new List<long>() { 2034573252755456L } },
                                { "Veteran 16", new List<long>() { 2034611907461120L } },
                            }
                        },
                        {
                            "Gharj", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 1783772932472832L } },
                                { "Story 16", new List<long>() { 2016946706972672L } },
                                { "Veteran 8", new List<long>() { 2034526008115200L } },
                                { "Veteran 16", new List<long>() { 2034534598049792L } },
                            }
                        },
                        {
                            "Soa", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 1783790112342016L } },
                                { "Story 16", new List<long>() { 2017170045272064L } },
                                { "Veteran 8", new List<long>() { 2289823159156736L } },
                                { "Veteran 16", new List<long>() { 2290085152161792L } },
                            }
                        }
                    };
                    break;
                case "Karagga's Palace":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Bonethrasher", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2271801476382720L } },
                                { "Story 16", new List<long>() { 2624491305828352L } },
                                { "Veteran 8", new List<long>() { 2624474125959168L } },
                                { "Veteran 16", new List<long>() { 2624508485697536L } },
                            }
                        },
                        {
                            "Jarg & Sorno", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2739437515571200L, 2739441810538496L } },
                                { "Story 16", new List<long>() { 2760500035190784L, 2760504330158080L } },
                                { "Veteran 8", new List<long>() { 2760482855321600L, 2760487150288896L } },
                                { "Veteran 16", new List<long>() { 2760517215059968L, 2760521510027264L } },
                            }
                        },
                        {
                            "Foreman Crusher", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2739875602235392L } },
                                { "Story 16", new List<long>() { 2739888487137280L } },
                                { "Veteran 8", new List<long>() { 2760637474144256L } },
                                { "Veteran 16", new List<long>() { 2760693308719104L } },
                            }
                        },
                        {
                            "G4-B3 Heavy Fabricator", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2747344550363136L } },
                                { "Story 16", new List<long>() { 2760371186171904L } },
                                { "Veteran 8", new List<long>() { 2748401112317952L } },
                                { "Veteran 16", new List<long>() { 2760375481139200L } },
                            }
                        }
                        ,
                        {
                            "Karagga the Unyielding", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2740043105959936L } },
                                { "Story 16", new List<long>() { 2761200114860032L } },
                                { "Veteran 8", new List<long>() { 2761191524925440L } },
                                { "Veteran 16", new List<long>() { 2761208704794624L } },
                            }
                        }
                    };
                    break;
                case "Explosive Conflict":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Zorn & Toth", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2788331423268864L, 2788335718236160L } },
                                { "Story 16", new List<long>() { 2860770341683200L, 2860766046715904L } },
                                { "Veteran 8", new List<long>() { 2857544821243904L, 2857549116211200L } },
                                { "Veteran 16", new List<long>() { 2861388816973824L, 2861384522006528L } },
                            }
                        },
                        {
                            "Firebrand & Stormcaller", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2808827007205376L, 2808831302172672L } },
                                { "Story 16", new List<long>() { 2876459857215488L, 2876464152182784L } },
                                { "Veteran 8", new List<long>() { 2876434087411712L, 2876438382379008L } },
                                { "Veteran 16", new List<long>() { 2876481332051968L, 2876485627019264L } },
                            }
                        },
                        {
                            "Colonel Vorgath", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2783478110224384L, 2813182104043520L } },
                                { "Story 16", new List<long>() { 2854156092047360L, 2854224811524096L } },
                                { "Veteran 8", new List<long>() { 2854151797080064L, 2854117437341696L } },
                                { "Veteran 16", new List<long>() { 2854160387014656L, 2854229106491392L } },
                            }
                        },
                        {
                            "Warlord Kephess", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2747344550363136L, 2800357331697664L,2794185463693312L} },
                                { "Story 16", new List<long>() { 2876545756561408L,2876550051528704L,2876562936430592L } },
                                { "Veteran 8", new List<long>() { 2876515691790336L, 2876528576692224L,2876532871659520L } },
                                { "Veteran 16", new List<long>() { 2876575821332480L, 2876588706234368L,2876593001201664L } },
                            }
                        }
                    };
                    break;
                case "Terror From Beyond":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "The Writhing Horror", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2962174519541760L } },
                                { "Story 16", new List<long>() { 3010428477112320L } },
                                { "Veteran 8", new List<long>() { 3010424182145024L } },
                                { "Veteran 16", new List<long>() { 3010432772079616L } },
                            }
                        },
                        {
                            "The Dread Guard", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2938011033534464L, 2938006738567168L, 2938002443599872L } },
                                { "Story 16", new List<long>() { 3013387709579264L, 3013379119644672L, 3013374824677376L } },
                                { "Veteran 8", new List<long>() { 3013336169971712L, 3013331875004416L, 3013327580037120L } },
                                { "Veteran 16", new List<long>() { 3013413479383040L, 3013409184415744L, 3013417774350336L } },
                            }
                        },
                        {
                            "Operator IX", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2939690365747200L, 2942606648541184L } },
                                { "Story 16", new List<long>() { 2994919350206464L, 2994850630729728L } },
                                { "Veteran 8", new List<long>() { 2994915055239168L, 2994837745827840L } },
                                { "Veteran 16", new List<long>() { 2994923645173760L, 2994859220664320L } },
                            }
                        },
                        {
                            "Kephess the Undying", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2937620191510528L } },
                                { "Story 16", new List<long>() { 3013134306508800L } },
                                { "Veteran 8", new List<long>() { 3013121421606912L } },
                                { "Veteran 16", new List<long>() { 3013138601476096L } },
                            }
                        }
                        ,
                        {
                            "The Terror From Beyond", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 2938891501830144L, 2938895796797440L, 2938887206862848L, 2978340776443904L } },
                                { "Story 16", new List<long>() { 3025263294152704L, 3025276179054592L, 3025224639447040L, 3025237524348928L } },
                                { "Veteran 8", new List<long>() { 3025271884087296L, 3025258999185408L, 3025220344479744L, 3025233229381632L } },
                                { "Veteran 16", new List<long>() { 3025280474021888L, 3025267589120000L, 3025228934414336L, 3025241819316224L } },
                            }
                        }
                    };
                    break;
                case "Scum and Villainy":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Dash'Roode", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3058837053505536L } },
                                { "Story 16", new List<long>() { 3153571147153408L } },
                                { "Veteran 8", new List<long>() { 3153558262251520L } },
                                { "Veteran 16", new List<long>() { 3153575442120704L } },
                            }
                        },
                        {
                            "Titan 6", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3016450021261312L } },
                                { "Story 16", new List<long>() { 3152463045591040L } },
                                { "Veteran 8", new List<long>() { 3152458750623744L } },
                                { "Veteran 16", new List<long>() { 3152467340558336L} },
                            }
                        },
                        {
                            "Thrasher", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3045819007631360L} },
                                { "Story 16", new List<long>() { 3154567579566080L } },
                                { "Veteran 8", new List<long>() { 3154563284598784L } },
                                { "Veteran 16", new List<long>() { 3154571874533376L } },
                            }
                        },
                        {
                            "Operations Chief", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3141940375715840L } },
                                { "Story 16", new List<long>() { 3157552581836800L } },
                                { "Veteran 8", new List<long>() { 3157548286869504L } },
                                { "Veteran 16", new List<long>() { 3157556876804096L } },
                            }
                        },
                        {
                            "Olok the Shadow", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3016445726294016L } },
                                { "Story 16", new List<long>() { 3154674953748480L } },
                                { "Veteran 8", new List<long>() { 3154662068846592L} },
                                { "Veteran 16", new List<long>() { 3154679248715776L } },
                            }
                        },
                        {
                            "Cartel Warlords", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3054413237190656L, 3054400352288768L, 3054404647256064L, 3054408942223360L } },
                                { "Story 16", new List<long>() { 3156899746807808L, 3054400352288768L, 3054404647256064L, 3054408942223360L } },
                                { "Veteran 8", new List<long>() { 3156895451840512L, 3054400352288768L, 3054404647256064L, 3054408942223360L } },
                                { "Veteran 16", new List<long>() { 3156904041775104L, 3054400352288768L, 3054404647256064L, 3054408942223360L } },
                            }
                        },
                        {
                            "Dread Master Styrak", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3066945951760384L, 3067057620910080,3225679353085952} },
                                { "Story 16", new List<long>() { 3152441570754560L, 3067057620910080,3225679353085952 } },
                                { "Veteran 8", new List<long>() { 3152407211016192L, 3067057620910080,3225679353085952 } },
                                { "Veteran 16", new List<long>() { 3152445865721856L, 3067057620910080,3225679353085952} },
                            }
                        }
                    };
                    break;
                case "The Dread Fortress":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Nefra, Who Bars the Way", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3266533082005504L } },
                                { "Story 16", new List<long>() { 3303036009054208L } },
                                { "Veteran 8", new List<long>() { 3303031714086912L } },
                                { "Veteran 16", new List<long>() { 3303040304021504L } },
                            }
                        },
                        {
                            "Gate Commander Draxus", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3273924720721920L} },
                                { "Story 16", new List<long>() { 3303401081274368L} },
                                { "Veteran 8", new List<long>() { 3303392491339776L } },
                                { "Veteran 16", new List<long>() { 3303405376241664L } },
                            }
                        },
                        {
                            "Grob'thok, Who Feeds the Forge", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3273929015689216L } },
                                { "Story 16", new List<long>() { 3302563562651648L } },
                                { "Veteran 8", new List<long>() { 3302559267684352L } },
                                { "Veteran 16", new List<long>() { 3302567857618944L } },
                            }
                        },
                        {
                            "Corruptor Zero", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3273933310656512L} },
                                { "Story 16", new List<long>() { 3303542815195136L } },
                                { "Veteran 8", new List<long>() { 3303534225260544L } },
                                { "Veteran 16", new List<long>() { 3303551405129728L } },
                            }
                        }
                        ,
                        {
                            "Dread Master Brontes", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3273937605623808L,3275625527771136L, 3277721471811584L } },
                                { "Story 16", new List<long>() { 3303538520227840L,3275625527771136L, 3277721471811584L } },
                                { "Veteran 8", new List<long>() { 3303529930293248L,3275625527771136L, 3277721471811584L } },
                                { "Veteran 16", new List<long>() { 3303547110162432L ,3275625527771136L, 3277721471811584L} },
                            }
                        }
                    };
                    break;
                case "The Dread Palace":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Dread Master Bestia", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3313331045662720L,3273941900591104L } },
                                { "Story 16", new List<long>() { 3313335340630016L,3273941900591104L } },
                                { "Veteran 8", new List<long>() { 3313339635597312L,3273941900591104L } },
                                { "Veteran 16", new List<long>() { 3313343930564608L,3273941900591104L } },
                            }
                        },
                        {
                            "Dread Master Tyrans", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3312841419390976L,3273954785492992L} },
                                { "Story 16", new List<long>() { 3312850009325568L,3273954785492992L} },
                                { "Veteran 8", new List<long>() { 3312845714358272L,3273954785492992L } },
                                { "Veteran 16", new List<long>() { 3312854304292864L,3273954785492992L } },
                            }
                        },
                        {
                            "Dread Master Calphayus", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3312983153311744L,3284949901770752L, 3284954196738048L, 3273946195558400L, 3349812497874944L } },
                                { "Story 16", new List<long>() { 3312991743246336L,3284949901770752L, 3284954196738048L, 3273946195558400L, 3349812497874944L } },
                                { "Veteran 8", new List<long>() { 3312987448279040L,3284949901770752L, 3284954196738048L, 3273946195558400L, 3349812497874944L } },
                                { "Veteran 16", new List<long>() { 3312996038213632L,3284949901770752L, 3284954196738048L, 3273946195558400L, 3349812497874944L } },
                            }
                        },
                        {
                            "Dread Master Raptus", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3273950490525696L} },
                                { "Story 16", new List<long>() { 3303555700097024L } },
                                { "Veteran 8", new List<long>() { 3302902865068032L } },
                                { "Veteran 16", new List<long>() { 3303559995064320L } },
                            }
                        }
                        ,
                        {
                            "Dread Council", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() {3273984850264064L,3273997735165952L, 3273989145231360L,3273993440198656L,3274019210002432L,3303830578003968L,3355541984247808L } },
                                { "Story 16", new List<long>() {3273984850264064L,3273997735165952L, 3273989145231360L,3273993440198656L,3274019210002432L,3303830578003968L,3355541984247808L} },
                                { "Veteran 8", new List<long>() {3273984850264064L,3273997735165952L, 3273989145231360L,3273993440198656L,3274019210002432L,3303830578003968L,3355541984247808L } },
                                { "Veteran 16", new List<long>() {3273984850264064L,3273997735165952L, 3273989145231360L,3273993440198656L,3274019210002432L,3303830578003968L,3355541984247808L} },
                            }
                        }
                    };
                    break;
                case "The Ravagers":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Sparky", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3367555007774720L } },
                                { "Story 16", new List<long>() { 3458114393210880L } },
                                { "Veteran 8", new List<long>() { 3458110098243584L } },
                                { "Veteran 16", new List<long>() { 3458122983145472L } },
                            }
                        },
                        {
                            "Quartermaster Bulo", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3371446248144896L} },
                                { "Story 16", new List<long>() { 3468701487595520L} },
                                { "Veteran 8", new List<long>() { 3468705782562816L } },
                                { "Veteran 16", new List<long>() { 3468697192628224L } },
                            }
                        },
                        {
                            "Torque", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3397005598523392L } },
                                { "Story 16", new List<long>() { 3468714372497408L } },
                                { "Veteran 8", new List<long>() { 3468710077530112L } },
                                { "Veteran 16", new List<long>() { 3468718667464704L } },
                            }
                        },
                        {
                            "Master & Blaster", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3391095723524096L,3391100018491392L} },
                                { "Story 16", new List<long>() { 3462181727240192L,3391100018491392L } },
                                { "Veteran 8", new List<long>() { 3458148752949248L,3391100018491392L } },
                                { "Veteran 16", new List<long>() { 3462186022207488L,3391100018491392L } },
                            }
                        }
                        ,
                        {
                            "Coratanni", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3443983950807040L,3371437658210304L, 3371441953177600L } },
                                { "Story 16", new List<long>() { 3468735847333888L,3371437658210304L, 3371441953177600L } },
                                { "Veteran 8", new List<long>() { 3468731552366592L,3371437658210304L, 3371441953177600L } },
                                { "Veteran 16", new List<long>() { 3468740142301184L ,3371437658210304L, 3371441953177600L} },
                            }
                        }
                    };
                    break;
                case "Temple of Sacrifice":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Malaphar the Savage", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3431245077807104L } },
                                { "Story 16", new List<long>() { 3469281308180480L } },
                                { "Veteran 8", new List<long>() { 3469277013213184L } },
                                { "Veteran 16", new List<long>() { 3469285603147776L } },
                            }
                        },
                        {
                            "Sword Squadron", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3447784996864000L,3447789291831296L} },
                                { "Story 16", new List<long>() { 3468770207072256L,3447789291831296L} },
                                { "Veteran 8", new List<long>() { 3468765912104960L,3447789291831296L } },
                                { "Veteran 16", new List<long>() { 3468774502039552L,3447789291831296L } },
                            }
                        },
                        {
                            "The Underlurker", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3411402328899584L } },
                                { "Story 16", new List<long>() { 3462267626586112L } },
                                { "Veteran 8", new List<long>() { 3462263331618816L } },
                                { "Veteran 16", new List<long>() { 3462271921553408L } },
                            }
                        },
                        {
                            "Revanite Commanders", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3482806160195584L,3456890327531520L, 3456894622498816L, 3456898917466112L} },
                                { "Story 16", new List<long>() { 3483029498494976L,3456890327531520L, 3456894622498816L, 3456898917466112L } },
                                { "Veteran 8", new List<long>() { 3483025203527680L,3456890327531520L, 3456894622498816L, 3456898917466112L } },
                                { "Veteran 16", new List<long>() { 3483033793462272L,3456890327531520L, 3456894622498816L, 3456898917466112L } },
                            }
                        }
                        ,
                        {
                            "Revan", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3443983950807040L,3431605855059968L, 3444310368321536L,3447583133401088L,3440805675008000L } },
                                { "Story 16", new List<long>() { 3468735847333888L,3431605855059968L, 3444310368321536L,3447583133401088L,3440805675008000L } },
                                { "Veteran 8", new List<long>() { 3468731552366592L,3431605855059968L, 3444310368321536L,3447583133401088L,3440805675008000L } },
                                { "Veteran 16", new List<long>() { 3468740142301184L ,3431605855059968L, 3444310368321536L,3447583133401088L,3440805675008000L} },
                            }
                        }
                    };
                    break;
                case "Toborro's Palace":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Golden Fury", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3210174521147392L } },
                                { "Story 16", new List<long>() { 3232800408862720L } },
                                { "Veteran 8", new List<long>() { 3232735984353280L } },
                                { "Veteran 16", new List<long>() { 3232817588731904L } },
                            }
                        }
                    };
                    break;
                case "Xeno":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Xenoanalyst II", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3153545377349632L } },
                                { "Story 16", new List<long>() { 3213924027596800L } },
                                { "Veteran 8", new List<long>() { 3213919732629504L } },
                                { "Veteran 16", new List<long>() { 3213928322564096L } },
                            }
                        }
                    };
                    break;
                case "Eyeless":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "The Eyeless", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3319090596806656L } },
                                { "Story 16", new List<long>() { 3328376316100608L } },
                                { "Veteran 8", new List<long>() { 3328372021133312L } },
                                { "Veteran 16", new List<long>() { 3328380611067904L } },
                            }
                        }
                    };
                    break;
                case "Queen's Hive":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Mutated Geonosian Queen", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4197299739688960L } },
                                { "Story 16", new List<long>() { 4204768687816704L } },
                                { "Veteran 8", new List<long>() { 4204764392849408L } },
                                { "Veteran 16", new List<long>() { 4204772982784000L } },
                            }
                        }
                    };
                    break;
                case "Colossal Monolith":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Colossal Monolith", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 3541140406009856L } },
                                { "Story 16", new List<long>() { 3570951774011392L } },
                                { "Veteran 8", new List<long>() { 3570947479044096L } },
                                { "Veteran 16", new List<long>() { 3570956068978688L } },
                            }
                        }
                    };
                    break;
                case "The Gods from the Machine":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "TYTH", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4078427929837568L } },
                                { "Story 16", new List<long>() { 4078419339902976L } },
                                { "Veteran 8", new List<long>() { 4078423634870272L } },
                                { "Veteran 16", new List<long>() { 4078415044935680L } },
                            }
                        },
                        {
                            "Aivela and Esne", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4088525397950464L,4088538282852352L} },
                                { "Story 16", new List<long>() { 4114282316824576L,4088538282852352L} },
                                { "Veteran 8", new List<long>() { 4114286611791872L,4088538282852352L } },
                                { "Veteran 16", new List<long>() { 4114290906759168L,4088538282852352L } },
                            }
                        },
                        {
                            "NAHUT", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4108011664572416L } },
                                { "Story 16", new List<long>() { 4122953855795200L } },
                                { "Veteran 8", new List<long>() { 4122949560827904L } },
                                { "Veteran 16", new List<long>() { 4122958150762496L } },
                            }
                        },
                        {
                            "SCYVA", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4108140513591296L} },
                                { "Story 16", new List<long>() { 4158786767945728L,4108140513591296L } },
                                { "Veteran 8", new List<long>() { 4158791062913024L,4108140513591296L } },
                                { "Veteran 16", new List<long>() { 4158859782389760L,4108140513591296L } },
                            }
                        }
                        ,
                        {
                            "IZAX", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4108097563918336L } },
                                { "Story 16", new List<long>() { 4163128979881984L,4108097563918336L } },
                                { "Veteran 8", new List<long>() { 4163133274849280L,4108097563918336L} },
                                { "Veteran 16", new List<long>() {4163137569816576L,4108097563918336L} },
                            }
                        }
                    };
                    break;
                case "Dxun":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "Red", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4246176467517440L } },
                                { "Story 16", new List<long>() { 4246176467517440L } },
                                { "Veteran 8", new List<long>() { 4330233272467456L } },
                                { "Veteran 16", new List<long>() { 4330233272467456L } },
                            }
                        },
                        {
                            "Trandosians", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4245970309087232L,4245978899021824L, 4245983193989120L, 4245987488956416L} },
                                { "Story 16", new List<long>() { 4245970309087232L,4245978899021824L, 4245983193989120L, 4245987488956416L} },
                                { "Veteran 8", new List<long>() { 4245970309087232L,4245978899021824L, 4245983193989120L, 4245987488956416L } },
                                { "Veteran 16", new List<long>() { 4245970309087232L,4245978899021824L, 4245983193989120L, 4245987488956416L } },
                                { "Master 8",new List<long>(){4381150109761536L,4245970309087232L,4245978899021824L, 4245983193989120L, 4245987488956416L}},
                                { "Master 16",new List<long>(){4381150109761536L,4245970309087232L,4245978899021824L, 4245983193989120L, 4245987488956416L}}
                            }
                        },
                        {
                            "Huntmaster", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4265104388390912L ,4281661487316992L} },
                                { "Story 16", new List<long>() { 4265104388390912L,4281661487316992L } },
                                { "Veteran 8", new List<long>() { 4265104388390912L ,4330237567434752L} },
                                { "Veteran 16", new List<long>() { 4265104388390912L ,4330237567434752L} },
                            }
                        },
                        {
                            "Apex Vanguard", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4282872668094464L} },
                                { "Story 16", new List<long>() { 4282872668094464L } },
                                { "Veteran 8", new List<long>() { 4350020186800128 } },
                                { "Veteran 16", new List<long>() { 4350020186800128 } },
                            }
                        }
                    };
                    break;
                case "R4":
                    encounter.BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                    {
                        {
                            "IP-CPT", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4467247024177152L,4480196350574592 ,4480200645541888} },
                                { "Veteran 8", new List<long>() { 4494653210492928L ,4480196350574592 ,4480200645541888} },
                            }
                        },
                        {
                            "Watchdog", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4466177577320448L} },
                                { "Veteran 8", new List<long>() { 4494700455133184L } },
                            }
                        },
                        {
                            "Lord Kanoth", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4466190462222336L } },
                                { "Veteran 8", new List<long>() { 4494876548792320L } },
                            }
                        },
                        {
                            "Lady Dominique", new Dictionary<string, List<long>>()
                            {
                                { "Story 8", new List<long>() { 4466199052156928L} },
                                { "Veteran 8", new List<long>() { 4608014577303552L,4488524292161536 } },
                            }
                        }
                    };
                    break;
            }
        }
    }
}