using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace EveOffline
{
    public sealed class EveOfflineRuntime : MonoBehaviour
    {
        enum Page { Fleet, Warehouse, Market, Academy, Service, Belt }

        static readonly Color Background = Hex("070b12");
        static readonly Color Panel = Hex("111b27");
        static readonly Color PanelAlt = Hex("172635");
        static readonly Color Border = Hex("294557");
        static readonly Color Ink = Hex("d9e7e7");
        static readonly Color Muted = Hex("7f99a2");
        static readonly Color Cyan = Hex("45dac9");
        static readonly Color ReadyGreen = Hex("68d889");
        static readonly Color MiningBeamBlue = Hex("4db8ff");
        static readonly Color Selection = Hex("fff176");
        static readonly Color Amber = Hex("e9b451");
        static readonly Color Danger = Hex("e65f6d");

        GameSave save;
        Font font;
        Canvas canvas;
        RectTransform pageRoot;
        Text walletText;
        Text operationText;
        Text noticeText;
        Text selectedAsteroidInfoText;
        Text npcRaidEtaText;
        Text miningActiveLocationText;
        Page page;
        string selectedPilotId;
        string selectedShipUid;
        string selectedAsteroidId;
        float simulationSpeed = 1f;
        float autosaveTimer;
        float marketTimer;
        string notice = "Домашняя станция Jita 4-4 готова.";
        float noticeSeconds = 8f;
        string offlineReportSummary = string.Empty;
        float offlineReportSeconds;
        GameObject offlineReportBanner;
        readonly Dictionary<string, GameObject> shipViews = new();
        readonly Dictionary<string, GameObject> asteroidViews = new();
        readonly Dictionary<string, GameObject> enemyViews = new();
        readonly Dictionary<string, LineRenderer> miningBeams = new();
        readonly Dictionary<string, Button> beltFleetButtons = new();
        readonly Dictionary<string, Text> beltFleetLabels = new();
        readonly List<AcademyQueueView> academyQueueViews = new();
        Button autoUnloadButton;
        Button autoRetargetButton;
        Button autoNextBeltButton;
        Text beltSiteStatusText;
        Transform worldRoot;
        Camera spaceCamera;
        RectTransform beltFleetPanel;
        int hangarPage;
        bool academyShowsCareerPlans;
        string academyCareerHullId = "venture";
        bool academyHullPickerOpen;
        PreparedPackageRole? academyCareerRole;
        string queueCopyConfirmationPilotId = string.Empty;
        string stationStateSignature = string.Empty;
        float stationStatePollSeconds;
        float miningDestinationRefreshSeconds;
        string expandedMiningSystemKey = string.Empty;
        bool scrollToExpandedMiningSystem;
        Text operationNavigationText;
        Text selectedPilotTransitText;
        Text miningTravelStatusText;
        string renderedSiteLocationId = string.Empty;
        int renderedSiteInstanceSerial;
        float academyProgressPollSeconds;
        string academyQueueSignature = string.Empty;
        Text academyPilotSummaryText;
        readonly List<MiningSystemView> miningSystemViews = new();
        readonly List<MiningDestinationView> miningDestinationViews = new();
        readonly List<FleetPilotStateView> fleetPilotStateViews = new();

        sealed class AcademyQueueView
        {
            public string SkillId;
            public int TargetLevel;
            public int QueueIndex;
            public Text Status;
            public RectTransform ProgressFill;
        }

        sealed class MiningSystemView
        {
            public string Key;
            public List<LocationDefinition> Locations;
            public LocationDefinition[] StaticBelts;
            public LocationDefinition[] Anomalies;
            public int SecurityTier;
            public int MaximumNpcCount;
            public Button Button;
            public Text Label;
            public Image Image;
        }

        sealed class MiningDestinationView
        {
            public LocationDefinition Location;
            public Button Button;
            public Text Label;
            public Image Image;
        }

        sealed class FleetPilotStateView
        {
            public string PilotId;
            public string ShipUid;
            public Text Label;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<EveOfflineRuntime>() == null)
                new GameObject("EVE Offline — Mining Command").AddComponent<EveOfflineRuntime>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            save = SaveService.LoadOrCreate();
            var offlineReport = OfflineSimulationService.LastOfflineReport;
            var preserveOfflineNotice = offlineReport?.HasElapsedTime == true;
            if (preserveOfflineNotice)
            {
                notice = offlineReport.Summary;
                noticeSeconds = 20f;
                offlineReportSummary = offlineReport.Summary;
                offlineReportSeconds = 20f;
            }
            selectedPilotId = save.Characters[0].Id;
            EnsureEventSystem();
            BuildCanvas();
            Open(Page.Fleet);
            StartCoroutine(MarketService.Refresh(save, (ok, message) =>
            {
                if (!ok || !preserveOfflineNotice || noticeSeconds <= 0) Tell(message);
                SaveService.Save(save);
            }));
        }

        void Update()
        {
            var realDt = Time.unscaledDeltaTime;
            SkillService.TickAll(save, realDt, Tell);
            OperationService.Tick(save, realDt * simulationSpeed, Tell);
            MiningSiteService.Tick(save);
            if(page==Page.Belt&&save.Operation?.Active==true&&!save.Operation.BeltWarpActive&&
               (!string.Equals(renderedSiteLocationId,save.Operation.LocationId,StringComparison.Ordinal)||renderedSiteInstanceSerial!=save.Operation.SiteInstanceSerial))
            {
                if(!save.Operation.Fleet.Any(member=>member.ShipUid==selectedShipUid))selectedShipUid=string.Empty;
                if(!save.Operation.Asteroids.Any(asteroid=>asteroid.Id==selectedAsteroidId&&asteroid.RemainingUnits>0))selectedAsteroidId=string.Empty;
                Open(Page.Belt);
                return;
            }
            if(page==Page.Belt&&save.Operation?.BeltWarpActive==true&&autoUnloadButton)Open(Page.Belt);
            var wasBeltWarp = save.Operation?.BeltWarpActive == true;
            if (TravelService.Tick(save, realDt * simulationSpeed, out var travelMessage))
            {
                Tell(travelMessage);
                SaveService.Save(save);
                if (page == Page.Belt && wasBeltWarp){selectedAsteroidId=string.Empty;Open(Page.Belt);}
                else if (page != Page.Belt) BuildStationAgain();
            }
            noticeSeconds -= realDt;
            offlineReportSeconds -= realDt;
            if(offlineReportSeconds<=0&&offlineReportBanner)
            {
                Destroy(offlineReportBanner);
                offlineReportBanner=null;
            }
            if(page!=Page.Belt&&(stationStatePollSeconds-=realDt)<=0)
            {
                stationStatePollSeconds=.25f;RefreshStationLiveStatus();var signature=StationStateSignature();
                if(!string.IsNullOrEmpty(stationStateSignature)&&signature!=stationStateSignature){stationStateSignature=signature;BuildStationAgain();return;}
                stationStateSignature=signature;
            }
            if(page==Page.Fleet&&(miningDestinationRefreshSeconds-=realDt)<=0)
            {
                miningDestinationRefreshSeconds=1f;
                RefreshMiningDestinationViews();
            }
            if(page==Page.Academy&&(academyProgressPollSeconds-=realDt)<=0)
            {
                academyProgressPollSeconds=.25f;
                if(!RefreshAcademyProgress())return;
            }
            autosaveTimer += realDt;
            marketTimer += realDt;
            if (autosaveTimer >= 10) { autosaveTimer = 0; SaveService.Save(save); }
            if (marketTimer >= 300) { marketTimer = 0; StartCoroutine(MarketService.Refresh(save, (ok, message) => { if (!ok) Tell(message); })); }
            UpdateHeader();
            if(page==Page.Belt&&save.Operation?.Active!=true){Open(Page.Fleet);return;}
            if (page == Page.Belt)
            {
                UpdateTacticalCamera(realDt);
                if(save.Operation.BeltWarpActive)SyncBeltWarpVisual();
                else{SyncWorld();RefreshBeltFleetHud();RefreshNpcRaidEta();}
                RefreshBeltSiteStatus();
            }
        }

        void OnApplicationQuit() => SaveService.Save(save);

        void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }

        void BuildCanvas()
        {
            var go = new GameObject("Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
        }

        void Open(Page next)
        {
            page = next;
            if (pageRoot) Destroy(pageRoot.gameObject);
            offlineReportBanner=null;
            if (worldRoot) Destroy(worldRoot.gameObject);
            if (spaceCamera) Destroy(spaceCamera.gameObject);
            shipViews.Clear(); asteroidViews.Clear(); enemyViews.Clear(); miningBeams.Clear();
            beltFleetButtons.Clear(); beltFleetLabels.Clear();academyQueueViews.Clear();miningSystemViews.Clear();miningDestinationViews.Clear();fleetPilotStateViews.Clear();academyPilotSummaryText=null;academyQueueSignature=string.Empty; beltFleetPanel = null;autoUnloadButton=null;autoRetargetButton=null;autoNextBeltButton=null;beltSiteStatusText=null;selectedAsteroidInfoText=null;npcRaidEtaText=null;miningActiveLocationText=null;operationNavigationText=null;selectedPilotTransitText=null;miningTravelStatusText=null;
            pageRoot = Rect("Page", canvas.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, next == Page.Belt ? Color.clear : Background);
            pageRoot.GetComponent<Image>().raycastTarget = next != Page.Belt;
            BuildStars(pageRoot);
            BuildHeader();
            if (next == Page.Belt) BuildBeltPage(); else { BuildStationBackdropCamera(); BuildStationNavigation(); BuildStationPage(); }
            if(next!=Page.Belt&&offlineReportSeconds>0&&!string.IsNullOrWhiteSpace(offlineReportSummary))BuildOfflineReportBanner();
            stationStateSignature=next==Page.Belt?string.Empty:StationStateSignature();stationStatePollSeconds=.25f;miningDestinationRefreshSeconds=0;
        }

        void BuildStationBackdropCamera()
        {
            worldRoot=new GameObject("Station backdrop camera root").transform;worldRoot.SetParent(transform);
            spaceCamera=new GameObject("Station Backdrop Camera").AddComponent<Camera>();spaceCamera.transform.SetParent(worldRoot);
            spaceCamera.clearFlags=CameraClearFlags.SolidColor;spaceCamera.backgroundColor=Background;spaceCamera.cullingMask=0;spaceCamera.depth=-100;
        }

        void BuildHeader()
        {
            var header = Rect("Header", pageRoot, new Vector2(0,-76), Vector2.zero, new Vector2(0,1), Vector2.one, new Color(.035f,.06f,.08f,.98f));
            Text(header, page == Page.Belt ? "MINING OPERATION" : "JITA IV — MOON 4", 22, Ink, new Vector2(24,-9), new Vector2(500,30), TextAnchor.MiddleLeft, FontStyle.Bold);
            Text(header, page == Page.Belt ? "Тактический контроль добывающего флота" : "Caldari Navy Assembly Plant // единая домашняя станция", 12, Muted, new Vector2(25,-41), new Vector2(600,22), TextAnchor.MiddleLeft);
            walletText = Text(header, "", 20, Amber, new Vector2(-520,-12), new Vector2(280,30), TextAnchor.MiddleRight, FontStyle.Bold); Pin(walletText.rectTransform, Vector2.one, Vector2.one);
            operationText = Text(header, "", 11, Muted, new Vector2(-520,-43), new Vector2(280,22), TextAnchor.MiddleRight); Pin(operationText.rectTransform, Vector2.one, Vector2.one);
            noticeText = Text(header, "", 12, Cyan, new Vector2(-225,-14), new Vector2(210,46), TextAnchor.MiddleRight); Pin(noticeText.rectTransform, Vector2.one, Vector2.one);
        }

        void BuildOfflineReportBanner()
        {
            var banner=Rect("Offline progress",pageRoot,new Vector2(260,-174),new Vector2(-30,-84),new Vector2(0,1),Vector2.one,new Color(.055f,.12f,.16f,.99f));
            offlineReportBanner=banner.gameObject;
            Text(banner,"ОФЛАЙН-ПРОГРЕСС",11,Cyan,new Vector2(18,-8),new Vector2(220,20),TextAnchor.UpperLeft,FontStyle.Bold);
            Text(banner,offlineReportSummary,12,Ink,new Vector2(18,-30),new Vector2(-122,52),TextAnchor.UpperLeft);
            var close=Button(banner,"ЗАКРЫТЬ",new Vector2(-62,-20),new Vector2(96,36),DismissOfflineReport,Border);
            Pin(close.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
        }

        void DismissOfflineReport()
        {
            offlineReportSeconds=0;
            if(offlineReportBanner)Destroy(offlineReportBanner);
            offlineReportBanner=null;
        }

        void BuildStationNavigation()
        {
            var nav = Rect("Navigation", pageRoot, new Vector2(0,0), new Vector2(230,-76), Vector2.zero, new Vector2(0,1), Panel);
            Text(nav,"СТАНЦИЯ",12,Cyan,new Vector2(18,-18),new Vector2(-18,24),TextAnchor.MiddleLeft,FontStyle.Bold);
            Nav(nav,"▦  ФЛОТ",62,Page.Fleet);
            Nav(nav,"▤  СКЛАД",108,Page.Warehouse);
            Nav(nav,"◫  РЫНОК JITA",154,Page.Market);
            Nav(nav,"◆  НАВЫКИ / КАРЬЕРА",200,Page.Academy);
            Text(nav,"ОПЕРАЦИЯ",11,Muted,new Vector2(18,-315),new Vector2(-18,20),TextAnchor.MiddleLeft,FontStyle.Bold);
            var operationButton=WideButton(nav, OperationNavigationLabel(), -348, () =>
            {
                if(save.Operation.BeltWarpActive){Open(Page.Belt);return;}
                if(save.Operation.TravelActive){Tell($"До прибытия {Mathf.CeilToInt(save.Operation.TravelSecondsLeft)} сек.");return;}
                if(save.Operation.Active){Open(Page.Belt);return;}
                BeginTravel(save.Operation.LocationId);
            }, save.Operation.TravelActive||save.Operation.BeltWarpActive ? Border : Amber);
            operationNavigationText=operationButton.GetComponentInChildren<Text>();
            Text(nav,"АВТОМАТИКА",11,Muted,new Vector2(18,-398),new Vector2(-18,20),TextAnchor.MiddleLeft,FontStyle.Bold);
            WideButton(nav,$"{(save.Operation.AutoUnload?"☑":"☐")}  АВТОВЫГРУЗКА",-430,()=>ToggleAutomation(true),save.Operation.AutoUnload?Cyan:PanelAlt);
            WideButton(nav,$"{(save.Operation.AutoRetarget?"☑":"☐")}  АВТОЦЕЛЬ ПРИ ПРОСТОЕ",-476,()=>ToggleAutomation(false),save.Operation.AutoRetarget?Cyan:PanelAlt);
            WideButton(nav,$"{(save.Operation.AutoNextBelt?"☑":"☐")}  {AutoRouteLabel()}",-522,ToggleAutoNextBelt,save.Operation.AutoNextBelt?Cyan:PanelAlt);
            Text(nav,"НОЧНОЙ ЦИКЛ: включи АВТОЦЕЛЬ + АВТОВЫГРУЗКУ + АВТОМАРШРУТ",10,Muted,new Vector2(18,-570),new Vector2(-18,58),TextAnchor.UpperLeft,FontStyle.Bold);
        }

        void BuildStationPage()
        {
            var content = Rect("Content", pageRoot, new Vector2(246,16), new Vector2(-16,-92), Vector2.zero, Vector2.one, new Color(.025f,.045f,.063f,.97f));
            switch (page)
            {
                case Page.Fleet: BuildFleet(content); break;
                case Page.Warehouse: BuildWarehouse(content); break;
                case Page.Market: BuildMarket(content); break;
                case Page.Academy: BuildAcademy(content); break;
                case Page.Service: BuildService(content); break;
            }
        }

        void BuildFleet(RectTransform content)
        {
            Title(content,"ФЛОТ И ПИЛОТЫ",$"{save.Characters.Count} пилотов • до {Catalog.FleetCapacity} кораблей в операции • один общий кошелёк");
            var left = Rect("Pilots",content,new Vector2(18,18),new Vector2(-420,-94),Vector2.zero,Vector2.one,Color.clear);
            var scroll = Scroll(left);
            for(var i=0;i<save.Characters.Count;i++)
            {
                var pilot=save.Characters[i];var ship=FindShip(pilot.AssignedShipUid);var selected=pilot.Id==selectedPilotId;
                const float fleetRowStep=60f;const float fleetRowHeight=56f;var top=-i*fleetRowStep;
                var row=Rect("Pilot",scroll.content,new Vector2(0,top-fleetRowHeight),new Vector2(0,top),new Vector2(0,1),Vector2.one,selected?new Color(.08f,.19f,.19f,1):PanelAlt);
                Text(row,$"{i+1:00}",12,Cyan,new Vector2(12,-7),new Vector2(36,40),TextAnchor.MiddleCenter,FontStyle.Bold);
                Text(row,pilot.Name,14,Ink,new Vector2(56,-5),new Vector2(190,23),TextAnchor.MiddleLeft,FontStyle.Bold);
                var stateText=Text(row,FleetPilotStateLabel(pilot,ship),10,ship==null?Danger:Muted,new Vector2(56,-30),new Vector2(180,18),TextAnchor.MiddleLeft);
                fleetPilotStateViews.Add(new FleetPilotStateView{PilotId=pilot.Id,ShipUid=ship?.Uid??string.Empty,Label=stateText});
                Text(row,ShipDisplayName(ship),14,ship==null?Muted:Amber,new Vector2(280,-10),new Vector2(240,30),TextAnchor.MiddleLeft,FontStyle.Bold);
                var id=pilot.Id; var b=Button(row,pilot.DeployOnLaunch?"В ВЫЛЕТЕ":"В РЕЗЕРВЕ",new Vector2(-218,-8),new Vector2(96,36),()=>{pilot.DeployOnLaunch=!pilot.DeployOnLaunch;SaveService.Save(save);BuildStationAgain();},pilot.DeployOnLaunch?Cyan:Border); Pin(b.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
                var select=Button(row,"ВЫБРАТЬ",new Vector2(-112,-8),new Vector2(98,36),()=>{selectedPilotId=id;BuildStationAgain();},selected?Cyan:Border); Pin(select.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
            }
            scroll.content.sizeDelta=new Vector2(0,save.Characters.Count*60+4);
            var details=Rect("Details",content,new Vector2(-390,18),new Vector2(-18,-94),new Vector2(1,0),Vector2.one,Panel);
            BuildPilotDetails(details,FindPilot(selectedPilotId));
        }

        void BuildPilotDetails(RectTransform panel,CharacterSave pilot)
        {
            if(pilot==null)return;var ship=FindShip(pilot.AssignedShipUid);var member=save.Operation.Fleet.Find(x=>x.PilotId==pilot.Id);
            Text(panel,pilot.Name,20,Ink,new Vector2(18,-18),new Vector2(-18,32),TextAnchor.MiddleLeft,FontStyle.Bold);
            Text(panel,$"Всего SP: {SkillService.TotalSp(pilot):N0}\nКомплект: {(ship==null?"нет":ShipDisplayName(ship))}\nМесто: {(ship==null?"Jita":ship.Location.ToString())}",12,Muted,new Vector2(18,-60),new Vector2(-18,82),TextAnchor.UpperLeft);
            if(ship==null)
            {
                Text(panel,"Купи готовый комплект на рынке и назначь его этому пилоту.",13,Muted,new Vector2(18,-160),new Vector2(-18,50),TextAnchor.UpperLeft);
            }
            else if(ship.Location==ShipLocation.Belt)
            {
                WideButton(panel,"ДОМОЙ И ОСТАТЬСЯ",-168,()=>{CommandReturnShip(ship.Uid,false);BuildStationAgain();},Amber);
                WideButton(panel,"РАЗГРУЗИТЬСЯ И ОБРАТНО",-216,()=>{CommandReturnShip(ship.Uid,true);BuildStationAgain();},Cyan);
            }
            else if(ship.Location==ShipLocation.Station && save.Operation.Active)
            {
                WideButton(panel,"ВЕРНУТЬ В ОПЕРАЦИЮ",-168,()=>{if(!OperationService.CanFly(pilot,ship))Tell("Нельзя вернуть: не изучены навыки корабля или полного готового комплекта.");else if(OperationService.Join(save,pilot.Id))Tell("Корабль вернулся в активную операцию.");else Tell("Нельзя вернуть: операция или вместимость флота не позволяет.");BuildStationAgain();},Cyan);
            }
            else if(save.Operation.BeltWarpActive && member!=null)
            {
                selectedPilotTransitText=Text(panel,SelectedPilotTransitLabel(),13,Amber,new Vector2(18,-160),new Vector2(-18,54),TextAnchor.UpperLeft,FontStyle.Bold);
            }
            else if(ship.Location==ShipLocation.Transit && save.Operation.TravelActive && save.Operation.TravelShipUids?.Contains(ship.Uid)==true)
            {
                selectedPilotTransitText=Text(panel,SelectedPilotTransitLabel(),13,Cyan,new Vector2(18,-160),new Vector2(-18,54),TextAnchor.UpperLeft,FontStyle.Bold);
            }
            WideButton(panel,"ОБСЛУЖИВАНИЕ КОРАБЛЯ",-278,()=>Open(Page.Service),Border);
            Text(panel,"МЕСТО ДОБЫЧИ",11,Cyan,new Vector2(18,-334),new Vector2(-18,22),TextAnchor.MiddleLeft,FontStyle.Bold);
            if(save.Operation.TravelActive||save.Operation.BeltWarpActive)
            {
                miningTravelStatusText=Text(panel,MiningTravelStatusLabel(),12,Amber,new Vector2(18,-362),new Vector2(-18,72),TextAnchor.UpperLeft,FontStyle.Bold);
            }
            else
            {
                var destinations=ScrollArea(panel,new Vector2(12,12),new Vector2(-12,-358));
                var active=save.Operation.Active?Catalog.GetLocation(save.Operation.LocationId):null;
                var listY=-4f;
                if(active!=null)
                {
                    miningActiveLocationText=Text(destinations.content,string.Empty,11,Muted,new Vector2(6,-2),new Vector2(-6,52),TextAnchor.UpperLeft);
                    listY=-66f;
                }

                var systems=Catalog.Locations
                    .GroupBy(location=>new { location.SystemId,location.SystemName })
                    .OrderBy(system=>MiningSiteService.SecurityTierTenths(system.First()))
                    .ThenBy(system=>system.Key.SystemName,StringComparer.OrdinalIgnoreCase)
                    .ThenBy(system=>system.Key.SystemId)
                    .ToList();
                var currentForExpansion=Catalog.GetLocation(save.Operation.LocationId)??Catalog.GetLocation(save.Operation.TravelDestinationLocationId);
                if(string.IsNullOrWhiteSpace(expandedMiningSystemKey))
                {
                    expandedMiningSystemKey=MiningSystemKey(currentForExpansion);
                    scrollToExpandedMiningSystem=true;
                }
                if(!systems.Any(system=>string.Equals(MiningSystemKey(system.First()),expandedMiningSystemKey,StringComparison.Ordinal)))
                    expandedMiningSystemKey=systems.Count>0?MiningSystemKey(systems[0].First()):string.Empty;

                var expandedRowY=0f;
                foreach(var system in systems)
                {
                    var systemLocations=system.OrderBy(location=>location.SiteKind)
                        .ThenBy(location=>location.BeltName,StringComparer.OrdinalIgnoreCase)
                        .ThenBy(location=>location.BeltId)
                        .ThenBy(location=>location.Id,StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var systemKey=MiningSystemKey(systemLocations[0]);
                    var expanded=string.Equals(systemKey,expandedMiningSystemKey,StringComparison.Ordinal);
                    if(expanded)expandedRowY=listY;
                    var capturedKey=systemKey;
                    var systemButton=WideButton(destinations.content,string.Empty,listY,()=>
                    {
                        expandedMiningSystemKey=capturedKey;
                        scrollToExpandedMiningSystem=true;
                        BuildStationAgain();
                    },expanded?Cyan:PanelAlt,true,52f);
                    miningSystemViews.Add(new MiningSystemView
                    {
                        Key=systemKey,
                        Locations=systemLocations,
                        StaticBelts=systemLocations.Where(location=>location.SiteKind==MiningSiteKind.StaticBelt).ToArray(),
                        Anomalies=systemLocations.Where(location=>location.SiteKind==MiningSiteKind.DynamicAnomaly).ToArray(),
                        SecurityTier=MiningSiteService.SecurityTierTenths(systemLocations[0]),
                        MaximumNpcCount=systemLocations.Where(location=>location.Threat>0&&location.MaxNpcCount>0).Select(location=>location.MaxNpcCount).DefaultIfEmpty(0).Max(),
                        Button=systemButton,
                        Label=systemButton.GetComponentInChildren<Text>(),
                        Image=systemButton.GetComponent<Image>()
                    });
                    listY-=60f;

                    if(expanded)
                    {
                        foreach(var location in systemLocations)
                        {
                            var id=location.Id;var selected=save.Operation.LocationId==id;
                            var button=WideButton(destinations.content,string.Empty,listY,()=>
                            {
                                if(selected&&save.Operation.Active){Open(Page.Belt);return;}
                                if(!MiningSiteService.IsAvailable(save,id)){Tell(MiningSiteService.AvailabilityText(save,id));return;}
                                if(save.Operation.Active)
                                {
                                    if(active!=null&&active.SystemId!=0&&active.SystemId==location.SystemId&&location.SiteKind==MiningSiteKind.StaticBelt)BeginSameSystemBeltWarp(id);
                                    else BeginTravel(id);
                                    return;
                                }
                                save.Operation.LocationId=id;SaveService.Save(save);Tell($"Выбран {location.DisplayName}.");BuildStationAgain();
                            },selected?Cyan:PanelAlt,true,60f);
                            // Security floor constrains automation only. Manual rows
                            // remain clickable even below it or while a site cools.
                            button.interactable=true;
                            miningDestinationViews.Add(new MiningDestinationView
                            {
                                Location=location,
                                Button=button,
                                Label=button.GetComponentInChildren<Text>(),
                                Image=button.GetComponent<Image>()
                            });
                            listY-=68f;
                        }
                    }
                    listY-=4f;
                }
                destinations.content.sizeDelta=new Vector2(0,Math.Max(320,-listY+8));
                if(scrollToExpandedMiningSystem&&!string.IsNullOrWhiteSpace(expandedMiningSystemKey))
                {
                    destinations.content.anchoredPosition=new Vector2(0,Math.Max(0,-expandedRowY-12f));
                    scrollToExpandedMiningSystem=false;
                }
                RefreshMiningDestinationViews();
            }
        }

        void RefreshMiningDestinationViews()
        {
            if(save?.Operation==null)return;
            if(miningActiveLocationText)
            {
                var active=save.Operation.Active?Catalog.GetLocation(save.Operation.LocationId):null;
                if(active!=null)
                {
                    var refresh=active.SiteKind==MiningSiteKind.StaticBelt?$"след. downtime {FormatUtc(MiningSiteService.NextStaticRefreshUnix())}":"динамическая аномалия";
                    miningActiveLocationText.text=$"Активно: {active.DisplayName}\n{MiningSiteService.AvailabilityText(save,active.Id)} • {refresh} • {AutoRouteLabel()}";
                }
            }
            foreach(var view in miningSystemViews)
            {
                if(view?.Button==null||view.Locations==null||view.Locations.Count==0)continue;
                var expanded=string.Equals(view.Key,expandedMiningSystemKey,StringComparison.Ordinal);
                if(view.Label)view.Label.text=MiningSystemLabel(view,expanded);
                if(view.Image)view.Image.color=expanded?Cyan:PanelAlt;
                if(view.Label)view.Label.color=expanded?Background:Ink;
            }

            foreach(var view in miningDestinationViews)
            {
                if(view?.Button==null||view.Location==null)continue;
                var location=view.Location;
                var selected=string.Equals(save.Operation.LocationId,location.Id,StringComparison.OrdinalIgnoreCase);
                var available=MiningSiteService.IsAvailable(save,location.Id);
                if(view.Label)view.Label.text=MiningDestinationLabel(location,selected);
                if(view.Image)view.Image.color=selected?Cyan:available?PanelAlt:Border;
                if(view.Label)view.Label.color=selected?Background:available?Ink:Muted;
                view.Button.interactable=true;
            }
        }

        string MiningSystemLabel(MiningSystemView view,bool expanded)
        {
            var first=view.Locations[0];
            var staticBelts=view.StaticBelts??Array.Empty<LocationDefinition>();
            var availableBelts=CountAvailableMiningLocations(staticBelts);
            var anomalies=view.Anomalies??Array.Empty<LocationDefinition>();
            var availableAnomalies=CountAvailableMiningLocations(anomalies);
            var npc=view.MaximumNpcCount<=0?"NPC НЕТ":$"NPC ДО {view.MaximumNpcCount}";
            var tier=view.SecurityTier;
            var manualNote=tier<AutoSecurityFloorTenths()?" • НИЖЕ ГРАНИЦЫ: ВРУЧНУЮ":string.Empty;
            var anomalySummary=anomalies.Length==0?"АНОМ. 0":$"АНОМ. {availableAnomalies}/{anomalies.Length}";
            return $"{(expanded?"▼":"▶")} {first.SystemName} • SEC {SecurityText(tier)} • {npc}\nБЕЛТЫ {availableBelts}/{staticBelts.Length} • {anomalySummary}{manualNote}";
        }

        int CountAvailableMiningLocations(IReadOnlyList<LocationDefinition> locations)
        {
            var count=0;
            for(var i=0;i<locations.Count;i++)
                if(MiningSiteService.IsAvailable(save,locations[i].Id))count++;
            return count;
        }

        string MiningDestinationLabel(LocationDefinition location,bool selected)
        {
            var status=MiningSiteService.AvailabilityText(save,location.Id);
            if(location.AllowedHullIds?.Length>0)
                status+=$" • только {string.Join(", ",location.AllowedHullIds.Select(hullId=>Catalog.GetShip(hullId)?.DisplayName??hullId))}";
            var mode=location.SiteKind==MiningSiteKind.DynamicAnomaly?"АНОМАЛИЯ":"БЕЛТ";
            var prefix=selected&&save.Operation.Active?"АКТИВНО • ":$"{mode} • ";
            var belowFloor=MiningSiteService.SecurityTierTenths(location)<AutoSecurityFloorTenths()?" • вручную можно":string.Empty;
            return $"{prefix}{location.BeltName}\n{LocationResourceSummary(location)} • {status} • {LocationCompatibilitySummary(location)}{belowFloor}";
        }

        static string MiningSystemKey(LocationDefinition location)=>location==null?string.Empty:$"{location.SystemId}:{location.SystemName}";

        int AutoSecurityFloorTenths()
        {
            if(save?.Operation?.AutoSecurityFloorInitialized==true)return save.Operation.AutoSecurityFloorTenths;
            var anchor=Catalog.GetLocation(save?.Operation?.LocationId)??Catalog.GetLocation(save?.Operation?.TravelSourceLocationId)??Catalog.GetLocation(save?.Operation?.TravelDestinationLocationId);
            return anchor==null?0:MiningSiteService.SecurityTierTenths(anchor);
        }

        string AutoRouteLabel()=>$"АВТОМАРШРУТ • SEC ≥ {SecurityText(AutoSecurityFloorTenths())}";
        static string SecurityText(int securityTenths)=>(securityTenths/10f).ToString("0.0",CultureInfo.InvariantCulture);

        string OperationNavigationLabel()
        {
            if(save.Operation.BeltWarpActive)
            {
                var destination=Catalog.GetLocation(save.Operation.BeltWarpDestinationLocationId);
                return $"{BeltWarpModeLabel(save.Operation,true)} • {destination?.SystemName??"МАРШРУТ"} • {Mathf.CeilToInt(save.Operation.BeltWarpSecondsLeft)}с";
            }
            if(save.Operation.TravelActive)
            {
                var destination=Catalog.GetLocation(save.Operation.TravelDestinationLocationId);
                return $"{(save.Operation.TravelIsAutomatic?"АВТОМАРШРУТ":"РУЧНОЙ ПЕРЕЛЁТ")} • {destination?.SystemName??"МАРШРУТ"} • {Mathf.CeilToInt(save.Operation.TravelSecondsLeft)}с";
            }
            return save.Operation.Active?"ОТКРЫТЬ АКТИВНОЕ МЕСТО":"ВЫЛЕТЕТЬ К МЕСТУ ДОБЫЧИ";
        }

        string FleetPilotStateLabel(CharacterSave pilot,ShipSave ship)
        {
            if(ship==null)return "БЕЗ КОРАБЛЯ";
            if(ship.Location==ShipLocation.Station)return "СТАНЦИЯ";
            var member=save.Operation.Fleet.Find(candidate=>candidate.PilotId==pilot?.Id);
            if(save.Operation.BeltWarpActive&&member!=null)
                return $"{(save.Operation.BeltWarpIsAutomatic?"АВТОМАРШРУТ":"ВАРП")} {Mathf.CeilToInt(save.Operation.BeltWarpSecondsLeft)}с";
            if(ship.Location==ShipLocation.Transit&&save.Operation.TravelActive&&save.Operation.TravelShipUids?.Contains(ship.Uid)==true)
                return $"{(save.Operation.TravelIsAutomatic?"АВТО":"В ПУТИ")} {Mathf.CeilToInt(save.Operation.TravelSecondsLeft)}с";
            return member?.Order.ToString().ToUpperInvariant()??"ТРАНЗИТ";
        }

        string SelectedPilotTransitLabel()
        {
            var pilot=FindPilot(selectedPilotId);
            var ship=FindShip(pilot?.AssignedShipUid);
            var member=save.Operation.Fleet.Find(candidate=>candidate.PilotId==pilot?.Id);
            if(save.Operation.BeltWarpActive&&member!=null)
            {
                var destination=Catalog.GetLocation(save.Operation.BeltWarpDestinationLocationId);
                return $"{BeltWarpModeLabel(save.Operation,false)} → {destination?.DisplayName}\nДо прибытия {Mathf.CeilToInt(save.Operation.BeltWarpSecondsLeft)} сек.";
            }
            if(ship?.Location==ShipLocation.Transit&&save.Operation.TravelActive&&save.Operation.TravelShipUids?.Contains(ship.Uid)==true)
            {
                var source=Catalog.GetLocation(save.Operation.TravelSourceLocationId);
                var destination=Catalog.GetLocation(save.Operation.TravelDestinationLocationId);
                return $"{(save.Operation.TravelIsAutomatic?"Автомаршрут":"Ручной перелёт")}: {source?.SystemName??"Jita"} → {destination?.DisplayName}\nДо прибытия {Mathf.CeilToInt(save.Operation.TravelSecondsLeft)} сек.";
            }
            return string.Empty;
        }

        string MiningTravelStatusLabel()
        {
            var beltWarp=save.Operation.BeltWarpActive;
            if(!beltWarp&&!save.Operation.TravelActive)return string.Empty;
            var destination=Catalog.GetLocation(beltWarp?save.Operation.BeltWarpDestinationLocationId:save.Operation.TravelDestinationLocationId);
            var source=Catalog.GetLocation(beltWarp?save.Operation.LocationId:save.Operation.TravelSourceLocationId);
            var progress=beltWarp?TravelService.BeltWarpProgress01(save.Operation):TravelService.Progress01(save.Operation);
            var seconds=beltWarp?save.Operation.BeltWarpSecondsLeft:save.Operation.TravelSecondsLeft;
            var count=beltWarp?(save.Operation.Fleet?.Count??0):(save.Operation.TravelShipUids?.Count??0);
            var mode=beltWarp?BeltWarpModeLabel(save.Operation,false):(save.Operation.TravelIsAutomatic?"АВТОМАРШРУТ":"РУЧНОЙ ПЕРЕЛЁТ");
            var pendingFloor=!beltWarp&&!save.Operation.TravelIsAutomatic&&destination!=null
                ?$" • после прибытия SEC ≥ {SecurityText(MiningSiteService.SecurityTierTenths(destination))}"
                :string.Empty;
            return $"{mode}: {source?.DisplayName??"Jita 4-4"} → {destination?.DisplayName}\n{count} кораблей • {Mathf.CeilToInt(seconds)} сек. • {progress:P0}{pendingFloor}";
        }

        void RefreshStationLiveStatus()
        {
            if(operationNavigationText)operationNavigationText.text=OperationNavigationLabel();
            foreach(var view in fleetPilotStateViews)
            {
                if(view?.Label==null)continue;
                view.Label.text=FleetPilotStateLabel(FindPilot(view.PilotId),FindShip(view.ShipUid));
            }
            if(selectedPilotTransitText)selectedPilotTransitText.text=SelectedPilotTransitLabel();
            if(miningTravelStatusText)miningTravelStatusText.text=MiningTravelStatusLabel();
        }

        static string BeltWarpModeLabel(OperationSave operation,bool compact)
        {
            var source=Catalog.GetLocation(operation?.LocationId);
            var destination=Catalog.GetLocation(operation?.BeltWarpDestinationLocationId);
            var sameSystem=source!=null&&destination!=null&&source.SystemId==destination.SystemId;
            if(operation?.BeltWarpIsAutomatic==true)return sameSystem?(compact?"АВТОВАРП":"АВТОМАРШРУТ • ВНУТРИ СИСТЕМЫ"):"АВТОМАРШРУТ";
            return sameSystem?"ЛОКАЛЬНЫЙ ВАРП":"ПЕРЕЛЁТ";
        }

        void BuildWarehouse(RectTransform content)
        {
            var items=save.StationInventory.Where(stack=>stack.Quantity>.0001).ToList();
            var totalOreValue=items.Sum(stack=>
            {
                var ore=ResourceForItem(stack.ItemId);
                return ore==null?0d:stack.Quantity*MarketService.OreBuyPerUnit(save,ore);
            });
            Title(content,"ОБЩИЙ СКЛАД",$"Оценка всех добытых ресурсов по Jita buy: {FormatIsk(totalOreValue)} • единый склад десяти пилотов");
            var scroll=ScrollArea(content,new Vector2(18,18),new Vector2(-18,-94));
            var instances=save.StationItemInstances.Where(instance=>instance!=null&&!string.IsNullOrWhiteSpace(instance.ItemId)).OrderBy(instance=>ItemName(instance.ItemId)).ThenByDescending(instance=>instance.Damage).ToList();
            const float warehouseRowHeight=72f;const float warehouseRowStep=76f;
            for(var i=0;i<items.Count;i++)
            {
                var stack=items[i];var resource=ResourceForItem(stack.ItemId);var row=Row(scroll.content,i,warehouseRowHeight);
                Text(row,ItemName(stack.ItemId),14,Ink,new Vector2(14,-6),new Vector2(260,26),TextAnchor.MiddleLeft,FontStyle.Bold);
                var volume=Catalog.TryGetItemVolumeM3(stack.ItemId,out var unitVolume)?stack.Quantity*unitVolume:0;
                var quantityText=$"{stack.Quantity:N0} ед. • {(volume>0?$"{volume:N1} м³":"объём неизвестен")}";
                if(resource?.Kind==ResourceKind.Ore)quantityText+=$"\n{OreGradeValueText(resource)}";
                Text(row,quantityText,11,Muted,new Vector2(285,-6),new Vector2(230,52),TextAnchor.UpperLeft);
                if(resource!=null)
                {
                    var unitPrice=MarketService.OreBuyPerUnit(save,resource);var stackPrice=stack.Quantity*unitPrice;
                    Text(row,$"Jita buy: {unitPrice:N2} ISK / ед.\nВесь запас: {stackPrice:N0} ISK",11,Amber,new Vector2(525,-7),new Vector2(430,52),TextAnchor.UpperLeft,FontStyle.Bold);
                    var local=stack;var sell=Button(row,"ПРОДАТЬ ВСЁ",new Vector2(-130,-16),new Vector2(112,40),()=>SellOre(local),Amber);Pin(sell.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
                }
                else Text(row,"Не является добываемым ресурсом • рыночная оценка здесь не считается",10,Muted,new Vector2(525,-7),new Vector2(430,26),TextAnchor.MiddleLeft);
            }
            for(var i=0;i<instances.Count;i++)
            {
                var instance=instances[i];var row=Row(scroll.content,items.Count+i,warehouseRowHeight);
                Text(row,ItemName(instance.ItemId),14,Ink,new Vector2(14,-7),new Vector2(300,28),TextAnchor.MiddleLeft,FontStyle.Bold);
                Text(row,$"Использованный экземпляр • износ {instance.Damage:P0} • {instance.Uid}",11,instance.Damage>=.8f?Danger:Muted,new Vector2(320,-7),new Vector2(520,28),TextAnchor.MiddleLeft);
            }
            var rowCount=items.Count+instances.Count;scroll.content.sizeDelta=new Vector2(0,Math.Max(80,rowCount*warehouseRowStep));
            if(rowCount==0)Text(scroll.content,"Склад пуст. Выйди на добычу и привези первый ресурс.",16,Muted,new Vector2(20,-40),new Vector2(-20,70),TextAnchor.MiddleCenter);
        }

        void BuildMarket(RectTransform content)
        {
            var updated=save.PriceCache.UpdatedUnix>0?DateTimeOffset.FromUnixTimeSeconds(save.PriceCache.UpdatedUnix).ToLocalTime().ToString("dd.MM HH:mm"):"нет снимка";
            Title(content,"РЫНОК JITA 4-4",$"Готовые добывающие комплекты • {save.PriceCache.Source} • обновлено {updated} • наши сделки рынок не меняют");
            var tabs=Rect("Market tabs",content,new Vector2(18,-78),new Vector2(-18,-36),new Vector2(0,1),Vector2.one,Color.clear);
            var refresh=Button(tabs,"ОБНОВИТЬ ЦЕНЫ",Vector2.zero,new Vector2(150,34),()=>StartCoroutine(MarketService.Refresh(save,(ok,msg)=>{Tell(msg);BuildStationAgain();})),Cyan);
            var scroll=ScrollArea(content,new Vector2(18,18),new Vector2(-18,-126));var products=new List<(string Category,string Name,string Summary,double Price,Action Buy,bool Ready)>();
            var pilot=FindPilot(selectedPilotId);
            var buyer=Text(tabs,$"ПОЛУЧАТЕЛЬ: {pilot?.Name??"не выбран"} • готовый пилот получает корабль, иначе он идёт в общий ангар",11,Muted,new Vector2(-620,-5),new Vector2(600,30),TextAnchor.MiddleRight,FontStyle.Bold);Pin(buyer.rectTransform,Vector2.one,Vector2.one);
            foreach(var package in Catalog.Packages)
            {
                var prepared=package;var skillsReady=PreparedPackageService.CanUsePackage(pilot,prepared);var slotFree=pilot!=null&&string.IsNullOrEmpty(pilot.AssignedShipUid);var ready=skillsReady&&slotFree;
                var skillState=ready?$"{pilot.Name}: навыки изучены • комплект будет назначен":pilot==null?"покупка в общий ангар":!skillsReady?"не хватает навыков • покупка в общий ангар":$"у {pilot.Name} уже есть корабль • покупка в общий ангар";
                products.Add((PackageRoleLabel(prepared.Role),prepared.DisplayName,$"{PreparedPackageService.EquipmentSummary(prepared)}\n{skillState}",PreparedPackageService.PackagePrice(save,prepared),()=>BuyPackage(prepared),ready));
            }
            const float marketRowHeight=86f;const float marketRowStep=90f;
            for(var i=0;i<products.Count;i++)
            {
                var product=products[i];var row=Row(scroll.content,i,marketRowHeight);
                Text(row,product.Category,9,Cyan,new Vector2(12,-7),new Vector2(82,28),TextAnchor.MiddleLeft,FontStyle.Bold);
                Text(row,product.Name,13,product.Ready?Ink:Amber,new Vector2(100,-6),new Vector2(-390,24),TextAnchor.MiddleLeft,FontStyle.Bold);
                Text(row,product.Summary,10,product.Ready?Muted:Amber,new Vector2(100,-34),new Vector2(-390,44),TextAnchor.UpperLeft);
                var free=product.Price<=.001;var priceText=Text(row,free?"БЕСПЛАТНО":$"{product.Price:N0} ISK",12,free?Cyan:Amber,new Vector2(-374,-12),new Vector2(240,28),TextAnchor.MiddleRight,FontStyle.Bold);Pin(priceText.rectTransform,Vector2.one,Vector2.one);
                var buy=Button(row,free?"ПОЛУЧИТЬ":"КУПИТЬ",new Vector2(-112,-23),new Vector2(96,40),product.Buy,product.Ready?Border:PanelAlt);Pin(buy.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
            }
            scroll.content.sizeDelta=new Vector2(0,products.Count*marketRowStep+8);
        }

        void BuildAcademy(RectTransform content)
        {
            Title(content,"НАВЫКИ И КАРЬЕРА","Пилот 01 живёт отдельно • Пилот 02 задаёт очередь рабочих • реальное обучение 2700 SP/час");
            var selector=Rect("Pilot select",content,new Vector2(18,18),new Vector2(210,-94),Vector2.zero,new Vector2(0,1),Panel);
            Text(selector,"ПИЛОТЫ • ВЫБЕРИ, КОГО СМОТРИМ",10,Cyan,new Vector2(10,-8),new Vector2(-10,24),TextAnchor.MiddleLeft,FontStyle.Bold);
            for(var i=0;i<save.Characters.Count;i++)
            {
                var p=save.Characters[i];var id=p.Id;var selected=id==selectedPilotId;var queueCount=SkillService.GetTrainingQueue(p).Count;var role=i==0?"НЕЗАВИСИМЫЙ • ИНДИВИД.":i==1?"ЭТАЛОН РАБОЧИХ":"РАБОЧИЙ";
                var label=$"{(selected?"▶ ":string.Empty)}{p.Name}\n{role} • очередь {queueCount}";var y=-58-i*52;
                var button=Button(selector,label,new Vector2(0,y),new Vector2(-16,44),()=>{selectedPilotId=id;queueCopyConfirmationPilotId=string.Empty;BuildStationAgain();},selected?Cyan:PanelAlt);Pin(button.GetComponent<RectTransform>(),new Vector2(0,1),Vector2.one);
            }
            var pilot=FindPilot(selectedPilotId)??save.Characters.FirstOrDefault();if(pilot==null)return;selectedPilotId=pilot.Id;
            var pilotIndex=save.Characters.IndexOf(pilot);var pilotRole=pilotIndex==0?"НЕЗАВИСИМЫЙ • индивидуальный план":pilotIndex==1?"ЭТАЛОН • его очередь копируется рабочим":"РАБОЧИЙ • получает очередь Пилота 02";var pilotShip=FindShip(pilot.AssignedShipUid);
            Text(selector,"СЕЙЧАС СМОТРИМ",10,Amber,new Vector2(10,-570),new Vector2(-10,20),TextAnchor.MiddleLeft,FontStyle.Bold);
            Text(selector,$"{pilot.Name}\n{pilotRole}\nКомплект: {(pilotShip==null?"не назначен":ShipDisplayName(pilotShip))}",10,Ink,new Vector2(10,-594),new Vector2(-10,64),TextAnchor.UpperLeft,FontStyle.Bold);
            var injector=Rect("Injector",content,new Vector2(228,-132),new Vector2(-18,-94),new Vector2(0,1),Vector2.one,Panel);
            var queue=SkillService.GetTrainingQueue(pilot);
            academyPilotSummaryText=Text(injector,string.Empty,9,Ink,new Vector2(14,-1),new Vector2(530,36),TextAnchor.MiddleLeft,FontStyle.Bold);
            var injectorSp=MarketService.InjectorSp(save,pilot);var injectorPrice=MarketService.InjectorPrice(save);var ib=Button(injector,$"LARGE +{injectorSp:N0} SP • {FormatIsk(injectorPrice)}",new Vector2(-122,-7),new Vector2(228,38),()=>{if(SkillService.TryBuyLargeSkillInjector(save,pilot,out var msg))SaveService.Save(save);Tell(msg);BuildStationAgain();},Amber);Pin(ib.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
            var smallInjectorSp=MarketService.SmallInjectorSp(save,pilot);var smallInjectorPrice=MarketService.SmallInjectorPrice(save);var smallIb=Button(injector,$"SMALL +{smallInjectorSp:N0} SP • {FormatIsk(smallInjectorPrice)}",new Vector2(-351,-7),new Vector2(218,38),()=>{if(SkillService.TryBuySmallSkillInjector(save,pilot,out var msg))SaveService.Save(save);Tell(msg);BuildStationAgain();},Border);Pin(smallIb.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
            var mode=Button(injector,academyShowsCareerPlans?"НАВЫКИ":"КОМПЛЕКТЫ",new Vector2(-501,-7),new Vector2(90,38),()=>{academyShowsCareerPlans=!academyShowsCareerPlans;BuildStationAgain();},Cyan);Pin(mode.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
            var scroll=ScrollArea(content,new Vector2(228,18),new Vector2(-18,-146));
            if(academyShowsCareerPlans)BuildCareerPlans(scroll.content,pilot);
            else
            {
                var isCommander=pilotIndex==0;var isWorkerTemplate=pilotIndex==1;var copyArmed=isWorkerTemplate&&queue.Count>0&&queueCopyConfirmationPilotId==pilot.Id;
                const float queueHeaderHeight=46f;const float copyPanelHeight=82f;const float queueRowHeight=82f;const float queueRowStep=86f;const float rightControlMargin=8f;var queueBodyTop=queueHeaderHeight+(copyArmed?copyPanelHeight:0);var queueBodyHeight=queue.Count>0?queue.Count*queueRowStep:38f;var queueSectionHeight=queueBodyTop+queueBodyHeight;
                var queueSection=Rect("Training queue",scroll.content,new Vector2(0,-queueSectionHeight),Vector2.zero,new Vector2(0,1),Vector2.one,Color.clear);
                var queueTitle=isCommander?"ОЧЕРЕДЬ ПИЛОТА 01 • ТОЛЬКО ЕГО":isWorkerTemplate?"ОЧЕРЕДЬ ПИЛОТА 02 • ЭТАЛОН РАБОЧИХ":"ОЧЕРЕДЬ РАБОЧЕГО • ПОЛУЧАТЕЛЬ ПЛАНА ПИЛОТА 02";
                Text(queueSection,queueTitle,12,Cyan,new Vector2(12,-8),new Vector2(500,28),TextAnchor.MiddleLeft,FontStyle.Bold);
                if(queue.Count>0)
                {
                    const float clearWidth=70f;var clear=Button(queueSection,"ОЧИСТИТЬ",new Vector2(-rightControlMargin-clearWidth*.5f,-queueHeaderHeight*.5f),new Vector2(clearWidth,32),()=>{if(SkillService.TryClearTrainingQueue(pilot,out var msg))SaveService.Save(save);Tell(msg);BuildStationAgain();},Danger);Pin(clear.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
                    if(isWorkerTemplate)
                    {
                        const float copyWidth=214f;var recipientCount=Math.Max(0,save.Characters.Count-2);var copy=Button(queueSection,copyArmed?"ОТМЕНИТЬ КОПИРОВАНИЕ":$"КОПИРОВАТЬ ОЧЕРЕДЬ РАБОЧИМ ×{recipientCount}",new Vector2(-rightControlMargin-clearWidth-8-copyWidth*.5f,-queueHeaderHeight*.5f),new Vector2(copyWidth,32),()=>{queueCopyConfirmationPilotId=copyArmed?string.Empty:pilot.Id;BuildStationAgain();},copyArmed?Amber:Cyan);Pin(copy.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
                    }
                    if(copyArmed)
                    {
                        var withBooks=SkillPlanService.PreviewCopyQueueToOtherWorkers(save,pilot,true);var withoutBooks=SkillPlanService.PreviewCopyQueueToOtherWorkers(save,pilot,false);
                        var confirm=Rect("Copy queue confirmation",queueSection,new Vector2(0,-queueHeaderHeight-copyPanelHeight),new Vector2(0,-queueHeaderHeight),new Vector2(0,1),Vector2.one,Panel);
                        Text(confirm,$"ПОЛНАЯ ЗАМЕНА очередей у {withBooks.TargetCount} рабочих (Пилоты 03–10) • Пилот 01 не участвует",11,Amber,new Vector2(12,-5),new Vector2(-12,22),TextAnchor.MiddleLeft,FontStyle.Bold);
                        Text(confirm,$"Копируется только очередь, не изученные SP. Книг: {withBooks.BooksPurchased} на {withBooks.BookCostIsk:N0} ISK; без покупки пропусков: {withoutBooks.EntriesSkipped}.",9,Muted,new Vector2(12,-27),new Vector2(-12,18),TextAnchor.MiddleLeft);
                        var buyAndCopy=Button(confirm,$"КНИГИ + КОПИЯ ОЧЕРЕДИ • {withBooks.BookCostIsk:N0} ISK",new Vector2(152,-47),new Vector2(280,30),()=>ApplyWorkerPlan(pilot,true),save.Isk+.001>=withBooks.BookCostIsk?Amber:Danger);Pin(buyAndCopy.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,1));
                        var copyOnly=Button(confirm,$"ТОЛЬКО ОЧЕРЕДЬ • ПРОПУСК {withoutBooks.EntriesSkipped}",new Vector2(415,-47),new Vector2(230,30),()=>ApplyWorkerPlan(pilot,false),Border);Pin(copyOnly.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,1));
                    }
                    for(var queueIndex=0;queueIndex<queue.Count;queueIndex++)
                    {
                        var entry=queue[queueIndex];var top=-queueBodyTop-queueIndex*queueRowStep;var queueRow=Rect("Queue row",queueSection,new Vector2(0,top-queueRowHeight),new Vector2(0,top),new Vector2(0,1),Vector2.one,PanelAlt);var queuedSkill=Catalog.GetSkill(entry.SkillId);var capturedIndex=queueIndex;
                        var levelStartSp=SkillService.RequiredSp(entry.SkillId,entry.TargetLevel-1);var levelEndSp=SkillService.RequiredSp(entry.SkillId,entry.TargetLevel);var levelCostSp=Math.Max(0,levelEndSp-levelStartSp);var savedSp=SkillService.GetState(pilot,entry.SkillId)?.SkillPoints??0;var levelProgressSp=Math.Clamp(savedSp-levelStartSp,0,levelCostSp);var progress01=levelCostSp>0?levelProgressSp/levelCostSp:1d;
                        Text(queueRow,$"{queueIndex+1}. {queuedSkill?.DisplayName??entry.SkillId} {SkillService.ToRoman(entry.TargetLevel)}  •  {levelCostSp:N0} SP",11,queueIndex==0?Cyan:Ink,new Vector2(12,-4),new Vector2(500,20),TextAnchor.MiddleLeft,queueIndex==0?FontStyle.Bold:FontStyle.Normal);
                        Text(queueRow,queuedSkill?.Description??"Описание навыка недоступно.",9,Muted,new Vector2(12,-25),new Vector2(-190,24),TextAnchor.UpperLeft);
                        var status=Text(queueRow,queueIndex==0?$"СЕЙЧАС • {levelProgressSp:N0}/{levelCostSp:N0} SP • осталось {FormatDuration(SkillService.TrainingSecondsLeft(pilot))}":$"ОЖИДАЕТ • {levelProgressSp:N0}/{levelCostSp:N0} SP",9,queueIndex==0?Cyan:Muted,new Vector2(12,-52),new Vector2(560,17),TextAnchor.MiddleLeft);
                        var progressTrack=Rect("Queue progress",queueRow,new Vector2(12,7),new Vector2(-174,11),new Vector2(0,0),new Vector2(1,0),Border);var progressFill=Rect("Queue progress fill",progressTrack,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2((float)progress01,1),queueIndex==0?Cyan:Muted);
                        academyQueueViews.Add(new AcademyQueueView{SkillId=entry.SkillId,TargetLevel=entry.TargetLevel,QueueIndex=queueIndex,Status=status,ProgressFill=progressFill});
                        const float moveWidth=38f;const float removeWidth=70f;const float buttonGap=6f;var controlY=-queueRowHeight*.5f;
                        var removeX=-rightControlMargin-removeWidth*.5f;var downX=-rightControlMargin-removeWidth-buttonGap-moveWidth*.5f;var upX=-rightControlMargin-removeWidth-buttonGap-moveWidth-buttonGap-moveWidth*.5f;
                        var up=Button(queueRow,"↑",new Vector2(upX,controlY),new Vector2(moveWidth,40),()=>MoveQueueEntry(pilot,capturedIndex,-1),queueIndex>0?Cyan:Panel);Pin(up.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
                        var down=Button(queueRow,"↓",new Vector2(downX,controlY),new Vector2(moveWidth,40),()=>MoveQueueEntry(pilot,capturedIndex,1),queueIndex<queue.Count-1?Cyan:Panel);Pin(down.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
                        var remove=Button(queueRow,"УБРАТЬ",new Vector2(removeX,controlY),new Vector2(removeWidth,40),()=>{if(SkillService.TryRemoveTrainingQueueEntry(pilot,capturedIndex,out var msg))SaveService.Save(save);Tell(msg);BuildStationAgain();},Border);Pin(remove.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
                    }
                }
                else Text(queueSection,"Очередь пуста — выбери + УРОВЕНЬ, ДО V или план готового комплекта.",11,Muted,new Vector2(12,-48),new Vector2(-12,28),TextAnchor.MiddleLeft);

                var skillsHeight=Catalog.Skills.Count*86+8;var skillsSection=Rect("Skill catalog",scroll.content,new Vector2(0,-queueSectionHeight-skillsHeight),new Vector2(0,-queueSectionHeight),new Vector2(0,1),Vector2.one,Color.clear);
                for(var i=0;i<Catalog.Skills.Count;i++)
                {
                    const float skillRowHeight=82f;var skill=Catalog.Skills[i];var state=SkillService.GetState(pilot,skill.Id);var level=SkillService.GetLevel(pilot,skill.Id);var training=pilot.TrainingSkillId==skill.Id;var planned=queue.Where(entry=>entry.SkillId==skill.Id).Select(entry=>entry.TargetLevel).DefaultIfEmpty(level).Max();var row=Row(skillsSection,i,skillRowHeight);
                    Text(row,skill.DisplayName,13,Ink,new Vector2(12,-4),new Vector2(250,22),TextAnchor.MiddleLeft,FontStyle.Bold);
                    Text(row,$"Rank {skill.Rank} • {SkillService.ToRoman(level)}"+(planned>level?$" → очередь {SkillService.ToRoman(planned)}":"")+(training?$" • сейчас {FormatDuration(SkillService.TrainingSecondsLeft(pilot))}":""),10,training?Cyan:Muted,new Vector2(12,-27),new Vector2(430,18),TextAnchor.MiddleLeft);
                    Text(row,skill.Description,9,Muted,new Vector2(12,-48),new Vector2(590,24),TextAnchor.UpperLeft);
                    var s=skill;
                    if(state?.BookOwned!=true){const float bookWidth=164f;var b=Button(row,$"КНИГА {MarketService.GetSellPrice(save,skill.TypeId,skill.BookFallbackPrice):N0}",new Vector2(-rightControlMargin-bookWidth*.5f,-skillRowHeight*.5f),new Vector2(bookWidth,42),()=>{if(SkillService.TryBuyBook(save,pilot,s.Id,out var msg))SaveService.Save(save);Tell(msg);BuildStationAgain();},Amber);Pin(b.GetComponent<RectTransform>(),Vector2.one,Vector2.one);}
                    else
                    {
                        const float trainWidth=78f;const float trainFiveWidth=72f;const float applyWidth=92f;const float controlGap=8f;var controlY=-skillRowHeight*.5f;
                        var applyX=-rightControlMargin-applyWidth*.5f;var trainFiveX=-rightControlMargin-applyWidth-controlGap-trainFiveWidth*.5f;var trainX=-rightControlMargin-applyWidth-controlGap-trainFiveWidth-controlGap-trainWidth*.5f;
                        var train=Button(row,level>=5?"ИЗУЧЕНО V":planned>=5?"В ОЧЕРЕДИ V":"+ УРОВЕНЬ",new Vector2(trainX,controlY),new Vector2(trainWidth,42),()=>{if(SkillService.TryEnqueueNextLevel(pilot,s.Id,out var msg))SaveService.Save(save);Tell(msg);BuildStationAgain();},training?Cyan:Border);Pin(train.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
                        var trainFive=Button(row,"ДО V",new Vector2(trainFiveX,controlY),new Vector2(trainFiveWidth,42),()=>{if(SkillService.TryEnqueueToTarget(pilot,s.Id,5,out var msg))SaveService.Save(save);Tell(msg);BuildStationAgain();},Border);Pin(trainFive.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
                        var apply=Button(row,"ВЛИТЬ SP",new Vector2(applyX,controlY),new Vector2(applyWidth,42),()=>{if(SkillService.TryApplyUnallocated(pilot,s.Id,out var msg))SaveService.Save(save);Tell(msg);BuildStationAgain();},pilot.UnallocatedSkillPoints>0?Amber:Border);Pin(apply.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
                    }
                }
                scroll.content.sizeDelta=new Vector2(0,queueSectionHeight+skillsHeight);
            }
            academyQueueSignature=TrainingQueueSignature(pilot);academyProgressPollSeconds=.25f;RefreshAcademyProgress(false);
        }

        void ApplyWorkerPlan(CharacterSave source,bool buyMissingBooks)
        {
            var workerTemplate=save?.Characters?.Skip(1).FirstOrDefault();
            if(source==null||workerTemplate==null||!string.Equals(source.Id,workerTemplate.Id,StringComparison.OrdinalIgnoreCase)){Tell("Копировать общую очередь можно только от Пилота 02 к Пилотам 03–10.");BuildStationAgain();return;}
            if(SkillPlanService.TryCopyQueueToOtherWorkers(save,source,buyMissingBooks,out var result)){queueCopyConfirmationPilotId=string.Empty;SaveService.Save(save);}
            Tell(result.Message);BuildStationAgain();
        }

        void MoveQueueEntry(CharacterSave pilot,int queueIndex,int direction)
        {
            if(SkillService.TryMoveTrainingQueueEntry(pilot,queueIndex,direction,out var message))SaveService.Save(save);
            Tell(message);BuildStationAgain();
        }

        string TrainingQueueSignature(CharacterSave pilot)=>pilot==null?string.Empty:$"{pilot.Id}|"+string.Join("|",SkillService.GetTrainingQueue(pilot).Select(entry=>$"{entry.SkillId}:{entry.TargetLevel}"));

        bool RefreshAcademyProgress(bool rebuildWhenQueueChanged=true)
        {
            if(page!=Page.Academy)return true;
            var pilot=FindPilot(selectedPilotId);if(pilot==null)return true;
            var signature=TrainingQueueSignature(pilot);
            if(rebuildWhenQueueChanged&&!string.IsNullOrEmpty(academyQueueSignature)&&signature!=academyQueueSignature){BuildStationAgain();return false;}
            if(academyPilotSummaryText)
            {
                var pilotIndex=save.Characters.IndexOf(pilot);var role=pilotIndex==0?"НЕЗАВИСИМЫЙ • индивидуальный план":pilotIndex==1?"ЭТАЛОН • очередь для рабочих":"РАБОЧИЙ • получает очередь Пилота 02";
                academyPilotSummaryText.text=$"СМОТРИМ: {pilot.Name} • {role}\n{SkillService.TotalSp(pilot):N0} SP • свободно {pilot.UnallocatedSkillPoints:N0} • очередь {SkillService.GetTrainingQueue(pilot).Count}/{SkillService.MaxTrainingQueueEntries} • ETA {FormatDuration(SkillService.TotalTrainingSecondsLeft(pilot))}";
            }
            foreach(var view in academyQueueViews)
            {
                var levelStartSp=SkillService.RequiredSp(view.SkillId,view.TargetLevel-1);var levelEndSp=SkillService.RequiredSp(view.SkillId,view.TargetLevel);var levelCostSp=Math.Max(0,levelEndSp-levelStartSp);var savedSp=SkillService.GetState(pilot,view.SkillId)?.SkillPoints??0;var levelProgressSp=Math.Clamp(savedSp-levelStartSp,0,levelCostSp);var progress01=levelCostSp>0?levelProgressSp/levelCostSp:1d;
                if(view.Status)view.Status.text=view.QueueIndex==0?$"СЕЙЧАС • {levelProgressSp:N0}/{levelCostSp:N0} SP • осталось {FormatDuration(SkillService.TrainingSecondsLeft(pilot))}":$"ОЖИДАЕТ • {levelProgressSp:N0}/{levelCostSp:N0} SP";
                if(view.ProgressFill)view.ProgressFill.anchorMax=new Vector2((float)progress01,1);
            }
            return true;
        }

        void BuildCareerPlans(Transform parent,CharacterSave pilot)
        {
            var availableHulls=Catalog.Ships.Where(hull=>Catalog.Packages.Any(package=>package.HullId==hull.Id)).ToList();
            var selectedHull=availableHulls.FirstOrDefault(hull=>hull.Id==academyCareerHullId)??availableHulls.FirstOrDefault();
            if(selectedHull==null){Text(parent,"Готовых комплектов пока нет.",14,Muted,new Vector2(18,-24),new Vector2(-18,40),TextAnchor.MiddleCenter);return;}
            academyCareerHullId=selectedHull.Id;
            var allPlans=CareerPlannerService.CreatePackagePlansForHull(save,pilot,selectedHull.Id);
            var availableRoles=allPlans.Where(plan=>plan.PackageRole.HasValue).Select(plan=>plan.PackageRole.Value).Distinct().ToList();
            if(!academyCareerRole.HasValue||!availableRoles.Contains(academyCareerRole.Value))academyCareerRole=availableRoles.FirstOrDefault();
            var plans=allPlans.Where(plan=>!academyCareerRole.HasValue||plan.PackageRole==academyCareerRole).ToList();

            var pickerRows=(availableHulls.Count+3)/4;var selectorHeight=academyHullPickerOpen?122+pickerRows*38:112;var selector=Rect("Career hull selector",parent,new Vector2(0,-selectorHeight),Vector2.zero,new Vector2(0,1),Vector2.one,Panel);
            Text(selector,selectedHull.DisplayName,16,Cyan,new Vector2(12,-8),new Vector2(330,24),TextAnchor.MiddleLeft,FontStyle.Bold);
            Text(selector,MiningPackageInfoService.HullDescription(selectedHull),10,Ink,new Vector2(12,-34),new Vector2(-260,38),TextAnchor.UpperLeft);
            Text(selector,$"{MiningPackageInfoService.BurstSummary(selectedHull)} • трюм {selectedHull.MiningHoldM3:N0} м³",10,Amber,new Vector2(12,-76),new Vector2(-260,22),TextAnchor.MiddleLeft,FontStyle.Bold);
            var hullPicker=Button(selector,$"КОРПУС: {selectedHull.DisplayName} {(academyHullPickerOpen?"▴":"▾")}",new Vector2(-125,-18),new Vector2(230,36),()=>{academyHullPickerOpen=!academyHullPickerOpen;BuildStationAgain();},Cyan);Pin(hullPicker.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
            if(academyHullPickerOpen)
            {
                for(var index=0;index<availableHulls.Count;index++)
                {
                    var choice=availableHulls[index];var capturedHullId=choice.Id;var column=index%4;var row=index/4;var choiceButton=Button(selector,choice.DisplayName,new Vector2(136+column*258,-118-row*38),new Vector2(248,32),()=>{academyCareerHullId=capturedHullId;academyCareerRole=null;academyHullPickerOpen=false;BuildStationAgain();},choice.Id==selectedHull.Id?Cyan:PanelAlt);Pin(choiceButton.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,1));
                }
            }

            const float roleHeight=42f;var roleBarTop=-selectorHeight-8;var roleBar=Rect("Career role filter",parent,new Vector2(0,roleBarTop-roleHeight),new Vector2(0,roleBarTop),new Vector2(0,1),Vector2.one,Color.clear);
            for(var roleIndex=0;roleIndex<availableRoles.Count;roleIndex++)
            {
                var role=availableRoles[roleIndex];var capturedRole=role;var roleButton=Button(roleBar,PackageRoleLabel(role),new Vector2(67+roleIndex*126,-20),new Vector2(118,34),()=>{academyCareerRole=capturedRole;BuildStationAgain();},academyCareerRole==role?Cyan:PanelAlt);Pin(roleButton.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,1));
            }

            const int massPilotColumns=5;const float massPilotCellHeight=34f;const float massPilotTop=168f;
            var workerPilotCount=Math.Max(0,save.Characters.Count-1);var massPilotRows=Math.Max(1,(workerPilotCount+massPilotColumns-1)/massPilotColumns);
            var planRowHeight=massPilotTop+massPilotRows*massPilotCellHeight+48f;var planRowStep=planRowHeight+4f;
            var plansTop=selectorHeight+roleHeight+16;var plansSection=Rect("Career package grades",parent,new Vector2(0,-plansTop-plans.Count*planRowStep),new Vector2(0,-plansTop),new Vector2(0,1),Vector2.one,Color.clear);
            for(var i=0;i<plans.Count;i++)
            {
                var plan=plans[i];var package=Catalog.GetPackage(plan.PackageId);var row=Row(plansSection,i,planRowHeight);var ready=plan.CanFlyNow;var missing=plan.Skills.Where(skill=>!skill.RequirementMet).ToList();
                Text(row,PackageGradeLabel(package),14,ready?Cyan:Ink,new Vector2(12,-5),new Vector2(285,24),TextAnchor.MiddleLeft,FontStyle.Bold);
                var role=plan.PackageRole.HasValue?PackageRoleLabel(plan.PackageRole.Value):"КОМПЛЕКТ";
                Text(row,ready?$"{role} • ГОТОВ":$"{role} • НЕ ХВАТАЕТ {missing.Count}",10,ready?Cyan:Amber,new Vector2(305,-7),new Vector2(175,22),TextAnchor.MiddleLeft,FontStyle.Bold);
                Text(row,$"SP {plan.RemainingSpAfterUnallocated:N0}",10,Muted,new Vector2(485,-7),new Vector2(115,22),TextAnchor.MiddleLeft);
                Text(row,$"Время {FormatDuration(plan.NaturalTrainingSeconds)}",10,Muted,new Vector2(600,-7),new Vector2(120,22),TextAnchor.MiddleLeft);
                Text(row,$"Книги {plan.MissingSkillBookCount} / {FormatIsk(plan.SkillBookCostIsk)}",10,Muted,new Vector2(720,-7),new Vector2(160,22),TextAnchor.MiddleLeft);
                Text(row,$"LSI {plan.LargeInjectorCount}",10,plan.LargeInjectorCount>0?Amber:Muted,new Vector2(880,-7),new Vector2(75,22),TextAnchor.MiddleLeft);
                var capturedPlan=plan;var queueButton=Button(row,ready?"ГОТОВО":"КНИГИ + ОЧЕРЕДЬ",new Vector2(-66,-17),new Vector2(116,30),()=>QueueCareerPlan(pilot,capturedPlan),ready?PanelAlt:Cyan);Pin(queueButton.GetComponent<RectTransform>(),Vector2.one,Vector2.one);
                var chain=missing.Count==0?"Все требования уже выполнены.":string.Join("  •  ",missing.Take(5).Select(skill=>$"{skill.DisplayName} {SkillService.ToRoman(skill.RequiredLevel)}"))+(missing.Count>5?$"  •  +{missing.Count-5}":"");
                Text(row,$"Оснащение: {PreparedPackageService.EquipmentSummary(package)}",9,Muted,new Vector2(12,-32),new Vector2(-12,18),TextAnchor.MiddleLeft);
                Text(row,$"Цепочка: {chain}",9,ready?Muted:Ink,new Vector2(12,-53),new Vector2(-12,18),TextAnchor.MiddleLeft);
                var packagePrice=plan.PackagePriceIsk<=.001?"БЕСПЛАТНО":FormatIsk(plan.PackagePriceIsk);
                Text(row,MiningPackageInfoService.YieldSummary(package)+" • минимум допуска → профильные навыки V • без внешних бурстов",10,Cyan,new Vector2(12,-76),new Vector2(-12,18),TextAnchor.MiddleLeft,FontStyle.Bold);
                Text(row,MiningPackageInfoService.CombatSummary(package),10,Ink,new Vector2(12,-98),new Vector2(-12,18),TextAnchor.MiddleLeft,FontStyle.Bold);
                Text(row,$"Комплект {packagePrice} • обучение сейчас {FormatIsk(plan.TotalInstantTrainingCostIsk)} • весь переход {FormatIsk(plan.PackagePriceIsk+plan.TotalInstantTrainingCostIsk)}",10,ready?Muted:Amber,new Vector2(12,-120),new Vector2(-12,20),TextAnchor.MiddleLeft,FontStyle.Bold);

                var massPreview=PreparedPackageService.PreviewAssignAll(save,package.Id);
                Text(row,$"МАССОВАЯ ПОСАДКА РАБОЧИХ • Пилот 01 исключён • зелёные подходят сейчас",9,Muted,new Vector2(12,-145),new Vector2(-12,18),TextAnchor.MiddleLeft,FontStyle.Bold);
                var independentPilotId=save.Characters.FirstOrDefault()?.Id;var workerPreviews=massPreview.Pilots.Where(candidate=>candidate!=null&&!string.Equals(candidate.PilotId,independentPilotId,StringComparison.OrdinalIgnoreCase)).ToList();
                for(var pilotIndex=0;pilotIndex<workerPreviews.Count;pilotIndex++)
                {
                    var pilotPreview=workerPreviews[pilotIndex];var column=pilotIndex%massPilotColumns;var pilotRow=pilotIndex/massPilotColumns;
                    var cell=Rect("Mass package pilot",row,new Vector2(6,-massPilotTop-massPilotCellHeight-pilotRow*massPilotCellHeight),new Vector2(-6,-massPilotTop-pilotRow*massPilotCellHeight),new Vector2(column/(float)massPilotColumns,1),new Vector2((column+1)/(float)massPilotColumns,1),Color.clear);
                    var pilotLabel=Text(cell,$"{pilotPreview.PilotName}\n{MassPackagePilotStatus(pilotPreview)}",9,pilotPreview.CanAssign?ReadyGreen:pilotPreview.Status==PreparedPackagePilotAssignmentStatus.UnsafeCurrentShip?Danger:Amber,Vector2.zero,Vector2.zero,TextAnchor.MiddleLeft,FontStyle.Bold);
                    pilotLabel.rectTransform.anchorMin=Vector2.zero;pilotLabel.rectTransform.anchorMax=Vector2.one;pilotLabel.rectTransform.offsetMin=pilotLabel.rectTransform.offsetMax=Vector2.zero;
                }
                var assignableCount=Math.Max(0,massPreview.EligibleCount-massPreview.AlreadyAssignedCount);var capturedPackage=package;
                var massAction=Button(row,MassPackageActionLabel(massPreview),new Vector2(12,-massPilotTop-massPilotRows*massPilotCellHeight-20),new Vector2(-12,36),()=>AssignPackageToAll(capturedPackage),assignableCount<=0?PanelAlt:massPreview.Affordable?Cyan:Danger);
                Pin(massAction.GetComponent<RectTransform>(),new Vector2(0,1),Vector2.one);
            }
            parent.GetComponent<RectTransform>().sizeDelta=new Vector2(0,plansTop+plans.Count*planRowStep+8);
        }

        static string MassPackagePilotStatus(PreparedPackagePilotAssignmentPreview pilot)
        {
            if(pilot==null)return "НЕДОСТУПЕН";
            return pilot.Status switch
            {
                PreparedPackagePilotAssignmentStatus.AlreadyAssigned=>"УЖЕ НА КОМПЛЕКТЕ",
                PreparedPackagePilotAssignmentStatus.MissingSkills=>"НЕ ХВАТАЕТ НАВЫКОВ",
                PreparedPackagePilotAssignmentStatus.UnsafeCurrentShip=>"СНАЧАЛА ВЕРНУТЬ НА СТАНЦИЮ",
                _=>pilot.WillUseHangarShip?"ГОТОВ • ИЗ АНГАРА":pilot.WillBuyShip?"ГОТОВ • БУДЕТ КУПЛЕН":"ГОТОВ"
            };
        }

        static string MassPackageActionLabel(PreparedPackageMassAssignmentResult preview)
        {
            if(preview==null||preview.EligibleCount<=0)return "НЕКОГО ИЗ РАБОЧИХ БЕЗОПАСНО ПЕРЕСАЖИВАТЬ";
            var assignable=Math.Max(0,preview.EligibleCount-preview.AlreadyAssignedCount);
            if(assignable==0)return $"ВСЕ ДОСТУПНЫЕ РАБОЧИЕ УЖЕ НА ЭТОМ КОМПЛЕКТЕ ×{preview.AlreadyAssignedCount}";
            var purchase=preview.PurchasedCount>0?$" • КУПИТЬ {preview.PurchasedCount} ЗА {FormatIsk(preview.PurchaseCostIsk)}":string.Empty;
            var affordability=!preview.Affordable&&preview.PurchasedCount>0?" • НЕ ХВАТАЕТ ISK":string.Empty;
            return $"ПОСАДИТЬ ГОТОВЫХ РАБОЧИХ ×{assignable} • ИЗ АНГАРА {preview.ReusedCount}{purchase}{affordability}";
        }

        void QueueCareerPlan(CharacterSave pilot,ShipCareerPlan plan)
        {
            if(pilot==null||plan==null)return;
            var freshPlan=CareerPlannerService.CreatePackagePlan(save,pilot,plan.PackageId);var missingBooks=freshPlan.Skills.Where(skill=>skill.NeedsBook).ToList();
            var requirements=freshPlan.Skills.Where(skill=>!skill.RequirementMet&&!SkillService.GetTrainingQueue(pilot).Any(entry=>entry.SkillId==skill.SkillId&&entry.TargetLevel>=skill.RequiredLevel)).ToList();
            var liveBookCost=missingBooks.Sum(skill=>MarketService.GetSellPrice(save,Catalog.GetSkill(skill.SkillId).TypeId,Catalog.GetSkill(skill.SkillId).BookFallbackPrice));
            if(save.Isk+0.001<liveBookCost){Tell($"Для книг плана {freshPlan.PackageName} нужно {FormatIsk(liveBookCost)}, в кошельке {FormatIsk(save.Isk)}.");return;}

            // Full dry run before a single ISK is spent: books are virtually injected
            // into a clone, then the entire prerequisite-first queue is validated.
            var preview=JsonUtility.FromJson<CharacterSave>(JsonUtility.ToJson(pilot));
            foreach(var skillPlan in missingBooks)SkillService.GetState(preview,skillPlan.SkillId,true).BookOwned=true;
            foreach(var skillPlan in requirements)
                if(!SkillService.TryEnqueueToTarget(preview,skillPlan.SkillId,skillPlan.RequiredLevel,out var preflightMessage)){Tell($"План {freshPlan.PackageName} не применён: {preflightMessage}");return;}

            var pilotIndex=save.Characters.IndexOf(pilot);var pilotBefore=JsonUtility.ToJson(pilot);var iskBefore=save.Isk;string failure=null;
            var booksBought=0;var entriesBefore=SkillService.GetTrainingQueue(pilot).Count;
            foreach(var skillPlan in missingBooks){if(SkillService.TryBuyBook(save,pilot,skillPlan.SkillId,out var bookMessage))booksBought++;else{failure=bookMessage;break;}}
            if(failure==null)foreach(var skillPlan in requirements)if(!SkillService.TryEnqueueToTarget(pilot,skillPlan.SkillId,skillPlan.RequiredLevel,out var queueMessage)){failure=queueMessage;break;}
            if(failure!=null)
            {
                save.Isk=iskBefore;if(pilotIndex>=0)save.Characters[pilotIndex]=JsonUtility.FromJson<CharacterSave>(pilotBefore);
                Tell($"План {freshPlan.PackageName} полностью отменён, ISK и очередь восстановлены: {failure}");BuildStationAgain();return;
            }
            var added=SkillService.GetTrainingQueue(pilot).Count-entriesBefore;
            SaveService.Save(save);Tell($"{freshPlan.PackageName}: куплено книг {booksBought}, добавлено пунктов очереди {Math.Max(0,added)}. Общий ETA {FormatDuration(SkillService.TotalTrainingSecondsLeft(pilot))}.");BuildStationAgain();
        }

        void BuildService(RectTransform content)
        {
            Title(content,"ОБСЛУЖИВАНИЕ КОРАБЛЯ","Готовый добывающий комплект заблокирован • здесь только ремонт, назначение и cargo");
            var pilot=FindPilot(selectedPilotId);var ship=FindShip(pilot?.AssignedShipUid);
            var left=Rect("Ship",content,new Vector2(18,18),new Vector2(460,-94),Vector2.zero,new Vector2(0,1),Panel);
            if(pilot==null)return;
            Text(left,pilot.Name,18,Ink,new Vector2(18,-18),new Vector2(-18,30),TextAnchor.MiddleLeft,FontStyle.Bold);
            if(ship==null)
            {
                Text(left,"Корабль не назначен. Купи готовый комплект на рынке или выбери его из общего ангара.",13,Muted,new Vector2(18,-65),new Vector2(-18,70),TextAnchor.UpperLeft);
                BuildHangar(left,pilot,-150);
                return;
            }

            var hull=Catalog.GetShip(ship.HullId);var maxHp=Math.Max(1f,PreparedPackageService.MaxTotalHp(ship,pilot));var currentHp=Math.Max(0,ship.ShieldHp+ship.ArmorHp+ship.StructureHp);var damagedHp=Math.Max(0,maxHp-currentHp);var repairCost=damagedHp*FittingService.RepairIskPerHp;
            Text(left,ShipDisplayName(ship),20,Amber,new Vector2(18,-58),new Vector2(-18,34),TextAnchor.MiddleLeft,FontStyle.Bold);
            Text(left,$"Место: {ship.Location}\nMining hold: {OperationService.HoldVolume(ship.MiningHold):N1}/{OperationService.MiningHoldCapacity(pilot,hull):N0} м³\nCargo: {OperationService.HoldVolume(ship.CargoHold):N1}/{hull.CargoHoldM3:N0} м³\nПрочность: {currentHp:N0}/{maxHp:N0} HP",12,Muted,new Vector2(18,-105),new Vector2(-18,130),TextAnchor.UpperLeft);
            if(ship.Location!=ShipLocation.Station)
            {
                Text(left,"Корабль можно обслуживать после возвращения на станцию.",13,Danger,new Vector2(18,-245),new Vector2(-18,46),TextAnchor.MiddleLeft,FontStyle.Bold);
            }
            else
            {
                WideButton(left,damagedHp>.01?$"РЕМОНТ {repairCost:N0} ISK • 100 ISK/HP":"РЕМОНТ НЕ НУЖЕН",-260,()=>RepairShip(pilot,ship),damagedHp>.01?Amber:PanelAlt);
                WideButton(left,"ПЕРЕМЕСТИТЬ КОРАБЛЬ В АНГАР",-306,()=>UnassignShip(pilot,ship),Border);
                BuildHangar(left,pilot,-364);
            }

            var right=Rect("Service",content,new Vector2(478,18),new Vector2(-18,-94),Vector2.zero,Vector2.one,Panel);
            var serviceScroll=ScrollArea(right,new Vector2(8,8),new Vector2(-8,-8));var service=serviceScroll.content;var serviceY=-16f;
            Text(service,"ЗАБЛОКИРОВАННЫЙ ГОТОВЫЙ КОМПЛЕКТ",12,Cyan,new Vector2(18,serviceY),new Vector2(800,24),TextAnchor.MiddleLeft,FontStyle.Bold);serviceY-=32;
            Text(service,LockedPackageSummary(ship),11,Ink,new Vector2(18,serviceY),new Vector2(-18,150),TextAnchor.UpperLeft);serviceY-=166;
            if(ship.Location!=ShipLocation.Station)
            {
                Text(service,"Логистика недоступна вне станции. Состав комплекта не меняется ни здесь, ни на рынке.",12,Muted,new Vector2(18,serviceY),new Vector2(-18,54),TextAnchor.UpperLeft);
                service.sizeDelta=new Vector2(0,Math.Max(420,-serviceY+90));
                return;
            }

            var bursts=ship.Modules.Where(module=>Catalog.GetModule(module.ModuleId)?.Kind==ModuleKind.MiningBurst).OrderBy(module=>module.Slot).ToList();
            if(bursts.Count>0)
            {
                Text(service,"MINING FOREMAN BURSTS • БЕЗ ЗАРЯДОВ • ВЕСЬ ФЛОТ",12,Cyan,new Vector2(18,serviceY),new Vector2(800,24),TextAnchor.MiddleLeft,FontStyle.Bold);serviceY-=32;
                for(var burstSlot=0;burstSlot<bursts.Count;burstSlot++)
                {
                    var burst=bursts[burstSlot];var moduleName=Catalog.GetModule(burst.ModuleId)?.DisplayName??burst.ModuleId;
                    Text(service,$"{burstSlot+1}. {moduleName} • встроенный эффект: {BurstProfileLabel(burst.ChargeId)}",11,Ink,new Vector2(18,serviceY),new Vector2(-18,28),TextAnchor.MiddleLeft,FontStyle.Bold);
                    serviceY-=34;
                }
                serviceY-=8;
            }
            var legacyFuel=FittingService.StackQuantity(ship.FuelHold,"heavy-water");
            if(legacyFuel>0)
            {
                Text(service,$"СТАРОЕ ТОПЛИВО • {legacyFuel:N0} Heavy Water • ЯДРУ БОЛЬШЕ НЕ НУЖНО",11,Amber,new Vector2(18,serviceY),new Vector2(-18,26),TextAnchor.MiddleLeft,FontStyle.Bold);serviceY-=34;
                WideButton(service,"ВЫГРУЗИТЬ СТАРОЕ ТОПЛИВО НА СКЛАД",serviceY,()=>TransferFuel(ship,double.PositiveInfinity,false),Border);serviceY-=54;
            }
            Text(service,"ОБЫЧНЫЙ CARGO",12,Cyan,new Vector2(18,serviceY),new Vector2(800,24),TextAnchor.MiddleLeft,FontStyle.Bold);serviceY-=32;
            Text(service,$"В отсеке {OperationService.HoldVolume(ship.CargoHold):N1}/{hull.CargoHoldM3:N0} м³. Покупки модулей, дронов и кристаллов больше не загружаются вручную.",11,Muted,new Vector2(18,serviceY),new Vector2(-18,44),TextAnchor.UpperLeft);serviceY-=52;
            WideButton(service,"ВЫГРУЗИТЬ ВЕСЬ CARGO НА ОБЩИЙ СКЛАД",serviceY,()=>UnloadAllCargo(ship),Border);serviceY-=54;
            service.sizeDelta=new Vector2(0,Math.Max(790,-serviceY+20));
        }

        void BuildBeltPage()
        {
            BuildSpaceWorld();
            var left=Rect("Fleet HUD",pageRoot,new Vector2(12,88),new Vector2(282,-88),Vector2.zero,new Vector2(0,1),new Color(.04f,.075f,.1f,.95f));
            beltFleetPanel=left;
            Text(left,"ФЛОТ В ЗОНЕ ДОБЫЧИ",15,Ink,new Vector2(14,-12),new Vector2(-14,28),TextAnchor.MiddleLeft,FontStyle.Bold);
            RefreshBeltFleetHud();
            WideButton(left,save.Operation.BeltWarpActive?"← СТАНЦИЯ (ВАРП ИДЁТ)":"← СТАНЦИЯ (ОПЕРАЦИЯ ИДЁТ)",18,()=>Open(Page.Fleet),Amber,false);
            var right=Rect("Commands",pageRoot,new Vector2(-298,88),new Vector2(-12,-88),new Vector2(1,0),Vector2.one,new Color(.04f,.075f,.1f,.95f));
            Text(right,"КОМАНДЫ",15,Ink,new Vector2(14,-12),new Vector2(-14,28),TextAnchor.MiddleLeft,FontStyle.Bold);
            npcRaidEtaText=Text(right,string.Empty,10,Danger,new Vector2(120,-12),new Vector2(-14,28),TextAnchor.MiddleRight,FontStyle.Bold);
            RefreshNpcRaidEta();
            beltSiteStatusText=Text(right,string.Empty,9,Muted,new Vector2(14,-34),new Vector2(-14,18),TextAnchor.MiddleLeft,FontStyle.Bold);
            RefreshBeltSiteStatus();
            if(save.Operation.BeltWarpActive)
            {
                var destination=Catalog.GetLocation(save.Operation.BeltWarpDestinationLocationId);
                Text(right,$"Флот варпает в {destination?.DisplayName}\nТрюмы, HP и состав флота сохраняются. ETA {Mathf.CeilToInt(save.Operation.BeltWarpSecondsLeft)} сек.",12,Amber,new Vector2(14,-78),new Vector2(-14,70),TextAnchor.UpperLeft,FontStyle.Bold);
                return;
            }
            WideButton(right,"ДОБЫВАТЬ ВЫБРАННУЮ ЦЕЛЬ",-54,TryIssueMiningOrder,Cyan);
            WideButton(right,"ДОМОЙ → РАЗГРУЗИТЬ → ОБРАТНО",-100,()=>CommandReturnShip(selectedShipUid,true),Amber);
            WideButton(right,"КОРАБЛЬ ДОМОЙ И ОСТАТЬСЯ",-146,()=>CommandReturnShip(selectedShipUid,false),Border);
            WideButton(right,"ВЕСЬ ФЛОТ: РАЗГРУЗИТЬ И ВЕРНУТЬ",-192,()=>CommandReturnFleet(true),Border);
            WideButton(right,"ВЕСЬ ФЛОТ: ДОМОЙ И ОСТАТЬСЯ",-238,()=>CommandReturnFleet(false),Border);
            autoUnloadButton=WideButton(right,string.Empty,-284,()=>ToggleAutomation(true),save.Operation.AutoUnload?Cyan:PanelAlt);
            autoRetargetButton=WideButton(right,string.Empty,-328,()=>ToggleAutomation(false),save.Operation.AutoRetarget?Cyan:PanelAlt);
            autoNextBeltButton=WideButton(right,string.Empty,-372,ToggleAutoNextBelt,save.Operation.AutoNextBelt?Cyan:PanelAlt);
            RefreshAutomationButtons();
            WideButton(right,"MINING FOREMAN BURST",-416,()=>{if(OperationService.ToggleBursts(save,selectedShipUid,out var msg))SaveService.Save(save);Tell(msg);},Cyan);
            WideButton(right,"INDUSTRIAL CORE",-460,()=>{if(OperationService.ToggleCore(save,selectedShipUid,out var msg))SaveService.Save(save);Tell(msg);},Danger);
            WideButton(right,"СЖАТЬ РЕСУРСЫ ФЛОТА",-504,CompressFleetMiningHolds,Amber);
            WideButton(right,"СКОРОСТЬ 1× / 5× / 20×",-548,()=>{simulationSpeed=simulationSpeed==1?5:simulationSpeed==5?20:1;Tell($"Скорость: {simulationSpeed:0}×");},PanelAlt);
            var asteroidCard=Rect("Selected asteroid",right,new Vector2(12,-676),new Vector2(-12,-594),new Vector2(0,1),Vector2.one,Panel);
            Text(asteroidCard,"ВЫБРАННАЯ ЦЕЛЬ ДОБЫЧИ",10,Cyan,new Vector2(12,-8),new Vector2(-12,20),TextAnchor.MiddleLeft,FontStyle.Bold);
            selectedAsteroidInfoText=Text(asteroidCard,string.Empty,10,Ink,new Vector2(12,-30),new Vector2(-12,52),TextAnchor.UpperLeft,FontStyle.Bold);
            RefreshSelectedAsteroidInfo();
            WideButton(right,"ОТВАРПАТЬ ФЛОТ И ЗАВЕРШИТЬ",18,()=>{if(OperationService.RequestStopWithWarp(save,out var msg))SaveService.Save(save);Tell(msg);},Danger,false);
        }

        void ToggleAutomation(bool unload)
        {
            if(unload)save.Operation.AutoUnload=!save.Operation.AutoUnload;
            else save.Operation.AutoRetarget=!save.Operation.AutoRetarget;
            SaveService.Save(save);
            Tell(unload
                ?$"Автовыгрузка {(save.Operation.AutoUnload?"включена":"выключена")}."
                :save.Operation.AutoRetarget
                    ?"Автопоиск цели включён: простаивающие добывающие корабли сами распределяются по доступным целям."
                    :"Автопоиск цели выключен.");
            if(page==Page.Belt)RefreshAutomationButtons();else BuildStationAgain();
        }

        void ToggleAutoNextBelt()
        {
            save.Operation.AutoNextBelt=!save.Operation.AutoNextBelt;
            SaveService.Save(save);
            Tell(save.Operation.AutoNextBelt
                ?$"Автомаршрут включён: SEC ≥ {SecurityText(AutoSecurityFloorTenths())}. Ночью также нужны АВТОЦЕЛЬ и АВТОВЫГРУЗКА."
                :"Автомаршрут выключен.");
            if(page==Page.Belt)RefreshAutomationButtons();else BuildStationAgain();
        }

        void CompressFleetMiningHolds()
        {
            if(OperationService.TryCompressFleetMiningHolds(save,selectedShipUid,out var result,out var message))
            {
                SaveService.Save(save);
                RefreshBeltHud();
            }
            Tell(message);
        }

        void RefreshAutomationButtons()
        {
            SetCheckButton(autoUnloadButton,save.Operation.AutoUnload,"АВТОВЫГРУЗКА ПРИ ПОЛНОМ ТРЮМЕ");
            SetCheckButton(autoRetargetButton,save.Operation.AutoRetarget,"ЕСЛИ НЕ КОПАЕТ → НАЙТИ КАМЕНЬ");
            SetCheckButton(autoNextBeltButton,save.Operation.AutoNextBelt,AutoRouteLabel());
        }

        void RefreshBeltSiteStatus()
        {
            if(!beltSiteStatusText)return;
            var current=Catalog.GetLocation(save.Operation?.LocationId);
            if(current==null){beltSiteStatusText.text="Место добычи не выбрано";return;}
            if(save.Operation.BeltWarpActive)
            {
                var destination=Catalog.GetLocation(save.Operation.BeltWarpDestinationLocationId);
                var source=Catalog.GetLocation(save.Operation.LocationId);
                beltSiteStatusText.text=$"{BeltWarpModeLabel(save.Operation,false)} • {source?.DisplayName} → {destination?.DisplayName} • {Mathf.CeilToInt(save.Operation.BeltWarpSecondsLeft)}с";
                beltSiteStatusText.color=Amber;
                return;
            }
            var timing=current.SiteKind==MiningSiteKind.StaticBelt
                ?$"DT {FormatUtc(MiningSiteService.NextStaticRefreshUnix())}"
                :MiningSiteService.AvailabilityText(save,current.Id);
            beltSiteStatusText.text=$"{current.SystemName} • SEC {SecurityText(MiningSiteService.SecurityTierTenths(current))} • {current.BeltName} • {timing} • {AutoRouteLabel()}";
            beltSiteStatusText.color=current.SiteKind==MiningSiteKind.DynamicAnomaly?Amber:Muted;
        }

        void RefreshNpcRaidEta()
        {
            if(!npcRaidEtaText)return;
            var location=Catalog.GetLocation(save.Operation?.LocationId);
            if(save.Operation?.Active!=true||location==null){npcRaidEtaText.text="NPC: операция не активна";npcRaidEtaText.color=Muted;return;}
            if(location.Threat<=0||location.MaxNpcCount<=0){npcRaidEtaText.text="NPC: не ожидаются";npcRaidEtaText.color=Cyan;return;}
            if((save.Operation.Enemies?.Count??0)>=location.MaxNpcCount){npcRaidEtaText.text=$"NPC: максимум {save.Operation.Enemies.Count}/{location.MaxNpcCount}";npcRaidEtaText.color=Danger;return;}
            if(OperationService.TryGetNextNpcRaidEta(save,out var seconds)){npcRaidEtaText.text=$"СЛЕД. NPC ЧЕРЕЗ {FormatCountdown(seconds)}";npcRaidEtaText.color=seconds<=30?Danger:Amber;return;}
            npcRaidEtaText.text="NPC: ETA неизвестно";npcRaidEtaText.color=Muted;
        }

        static void SetCheckButton(Button button,bool enabled,string label)
        {
            if(!button)return;var color=enabled?Cyan:PanelAlt;var image=button.GetComponent<Image>();if(image)image.color=color;
            var text=button.GetComponentInChildren<Text>();if(text){text.text=$"{(enabled?"☑":"☐")}  {label}";text.color=enabled?Background:Ink;}
            var colors=button.colors;colors.highlightedColor=Color.Lerp(color,Color.white,.15f);colors.pressedColor=Color.Lerp(color,Color.black,.2f);button.colors=colors;
        }

        void BuildSpaceWorld()
        {
            renderedSiteLocationId=save.Operation?.LocationId??string.Empty;
            renderedSiteInstanceSerial=save.Operation?.SiteInstanceSerial??0;
            worldRoot=new GameObject("Persistent operation view").transform;worldRoot.SetParent(transform);
            spaceCamera=new GameObject("Tactical Camera").AddComponent<Camera>();spaceCamera.transform.SetParent(worldRoot);spaceCamera.transform.position=new Vector3(0,62,-68);spaceCamera.transform.rotation=Quaternion.Euler(40,0,0);spaceCamera.fieldOfView=52;spaceCamera.clearFlags=CameraClearFlags.SolidColor;spaceCamera.backgroundColor=Background;spaceCamera.gameObject.AddComponent<AudioListener>();
            var light=new GameObject("Light").AddComponent<Light>();light.transform.SetParent(worldRoot);light.type=LightType.Directional;light.intensity=1.25f;light.color=new Color(.75f,.88f,1);light.transform.rotation=Quaternion.Euler(40,-30,0);
            foreach(var a in save.Operation.Asteroids.Where(x=>x.RemainingUnits>0)){var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=a.Id;go.transform.SetParent(worldRoot);go.transform.position=new Vector3(a.X,a.Y,a.Z);go.transform.localScale=new Vector3(a.Scale,a.Scale*.72f,a.Scale*1.15f);go.transform.rotation=UnityEngine.Random.rotation;Paint(go,Catalog.GetOre(a.OreId).Color);var id=a.Id;go.AddComponent<WorldClickable>().Clicked=()=>{selectedAsteroidId=id;var asteroid=save.Operation.Asteroids.Find(x=>x.Id==id);RefreshSelectedAsteroidInfo();Tell(AsteroidInfo(asteroid).Replace('\n',' '));};asteroidViews[id]=go;}
        }

        void SyncWorld()
        {
            var liveFleetUids=new HashSet<string>(save.Operation.Fleet.Where(member=>!string.IsNullOrEmpty(member.ShipUid)).Select(member=>member.ShipUid));
            foreach(var member in save.Operation.Fleet)
            {
                var ship=FindShip(member.ShipUid);
                var warpVisible=ship!=null&&ship.Location==ShipLocation.Transit&&member.WarpPhase is FleetWarpPhase.AligningOut or FleetWarpPhase.WarpingIn;
                var warpHidden=ship!=null&&ship.Location==ShipLocation.Transit&&member.WarpPhase==FleetWarpPhase.InTransit;
                if(ship==null||(ship.Location!=ShipLocation.Belt&&!warpVisible&&!warpHidden))
                {
                    if(shipViews.Remove(member.ShipUid,out var gone))Destroy(gone);
                    if(miningBeams.Remove(member.ShipUid,out var oldBeam))Destroy(oldBeam.gameObject);
                    continue;
                }
                var hull=Catalog.GetShip(ship.HullId);
                if(hull==null)continue;
                if(warpHidden)
                {
                    if(shipViews.TryGetValue(member.ShipUid,out var hiddenShip)&&hiddenShip)hiddenShip.SetActive(false);
                    if(miningBeams.TryGetValue(member.ShipUid,out var hiddenBeam))hiddenBeam.enabled=false;
                    continue;
                }
                if(!shipViews.TryGetValue(member.ShipUid,out var go))
                {
                    go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=member.ShipUid;go.transform.SetParent(worldRoot);
                    Paint(go,hull.IsCommandShip?Amber:Cyan);
                    ConfigureWarpTransparency(go);
                    var uid=member.ShipUid;go.AddComponent<WorldClickable>().Clicked=()=>{selectedShipUid=uid;RefreshBeltHud();};shipViews[member.ShipUid]=go;
                }
                if(!go.activeSelf)go.SetActive(true);
                var selected=member.ShipUid==selectedShipUid;var scale=hull.IsCommandShip?2.2f:hull.Class is ShipClass.MiningBarge or ShipClass.Exhumer?1.4f:1f;
                var normalScale=new Vector3(scale*.6f,scale*.4f,scale*2.1f)*(selected?1.18f:1f);
                var normalColor=selected?Selection:hull.IsCommandShip?Amber:Cyan;
                if(member.WarpPhase!=FleetWarpPhase.None)
                {
                    ApplyWarpVisual(member,go,normalScale,normalColor);
                    if(miningBeams.TryGetValue(member.ShipUid,out var warpBeam))warpBeam.enabled=false;
                    continue;
                }
                SetWarpColliderEnabled(go,true);
                go.transform.localScale=normalScale;
                SetPaintColor(go,normalColor);
                go.transform.position=new Vector3(member.X,member.Y,member.Z);
                if(!string.IsNullOrEmpty(member.TargetAsteroidId)&&asteroidViews.TryGetValue(member.TargetAsteroidId,out var target)){go.transform.LookAt(target.transform.position);SyncMiningBeam(member,go,target);}
                else if(miningBeams.TryGetValue(member.ShipUid,out var beam))beam.enabled=false;
            }
            foreach(var uid in shipViews.Keys.Where(uid=>!liveFleetUids.Contains(uid)).ToArray()){if(shipViews.Remove(uid,out var gone))Destroy(gone);if(miningBeams.Remove(uid,out var oldBeam))Destroy(oldBeam.gameObject);}
            foreach(var asteroid in save.Operation.Asteroids.Where(x=>x.RemainingUnits<=0)){if(selectedAsteroidId==asteroid.Id)selectedAsteroidId=string.Empty;if(asteroidViews.Remove(asteroid.Id,out var gone))Destroy(gone);}
            foreach(var enemy in save.Operation.Enemies){if(!enemyViews.TryGetValue(enemy.Id,out var go)){go=GameObject.CreatePrimitive(PrimitiveType.Capsule);go.transform.SetParent(worldRoot);go.transform.localScale=new Vector3(.65f,.65f,1.3f);Paint(go,Danger);enemyViews[enemy.Id]=go;}go.transform.position=new Vector3(enemy.X,enemy.Y,enemy.Z);}foreach(var key in enemyViews.Keys.Where(id=>save.Operation.Enemies.All(e=>e.Id!=id)).ToArray()){Destroy(enemyViews[key]);enemyViews.Remove(key);}
            RefreshSelectedAsteroidInfo();
        }

        void SyncBeltWarpVisual()
        {
            var op=save.Operation;if(op?.BeltWarpActive!=true)return;
            foreach(var asteroid in asteroidViews.Values)if(asteroid)asteroid.SetActive(false);
            foreach(var enemy in enemyViews.Values)if(enemy)enemy.SetActive(false);
            foreach(var beam in miningBeams.Values)if(beam)beam.enabled=false;

            var progress=TravelService.BeltWarpProgress01(op);
            var warpPoint=op.WarpPointInitialized
                ?new Vector3(op.WarpPointX,op.WarpPointY,op.WarpPointZ)
                :new Vector3(105f,14f,120f);
            var liveFleetUids=new HashSet<string>();
            for(var index=0;index<op.Fleet.Count;index++)
            {
                var member=op.Fleet[index];var ship=FindShip(member.ShipUid);var hull=Catalog.GetShip(ship?.HullId);
                if(ship==null||hull==null)continue;
                liveFleetUids.Add(member.ShipUid);
                if(!shipViews.TryGetValue(member.ShipUid,out var go))
                {
                    go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=member.ShipUid;go.transform.SetParent(worldRoot);
                    Paint(go,hull.IsCommandShip?Amber:Cyan);ConfigureWarpTransparency(go);var uid=member.ShipUid;go.AddComponent<WorldClickable>().Clicked=()=>{selectedShipUid=uid;RefreshBeltHud();};shipViews[member.ShipUid]=go;
                }
                SetWarpColliderEnabled(go,false);
                var selected=member.ShipUid==selectedShipUid;var scale=hull.IsCommandShip?2.2f:hull.Class is ShipClass.MiningBarge or ShipClass.Exhumer?1.4f:1f;
                var normalScale=new Vector3(scale*.6f,scale*.4f,scale*2.1f)*(selected?1.18f:1f);
                var normalColor=selected?Selection:hull.IsCommandShip?Amber:Cyan;
                var origin=new Vector3(member.X,member.Y,member.Z);
                var destination=new Vector3(-22+(index%3)*3.2f,-2+(index/3)*2.2f,-4+(index%2)*2);

                if(progress<.45f)
                {
                    if(!go.activeSelf)go.SetActive(true);
                    var t=Mathf.Clamp01(progress/.45f);var acceleration=t*t*t;
                    go.transform.position=Vector3.LerpUnclamped(origin,warpPoint,acceleration);
                    var direction=warpPoint-go.transform.position;if(direction.sqrMagnitude>.0001f)go.transform.rotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
                    var streak=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.35f,1f,t));
                    go.transform.localScale=new Vector3(normalScale.x*(1f-.35f*streak),normalScale.y*(1f-.35f*streak),normalScale.z*(1f+5f*streak));
                    var color=Color.Lerp(normalColor,Color.white,streak*.85f);color.a=Mathf.Lerp(1f,.05f,Mathf.SmoothStep(0f,1f,t));SetPaintColor(go,color);
                }
                else if(progress<=.55f)go.SetActive(false);
                else
                {
                    if(!go.activeSelf)go.SetActive(true);
                    var t=Mathf.Clamp01((progress-.55f)/.45f);var deceleration=1f-Mathf.Pow(1f-t,3f);
                    go.transform.position=Vector3.LerpUnclamped(warpPoint,destination,deceleration);
                    var direction=destination-go.transform.position;if(direction.sqrMagnitude>.0001f)go.transform.rotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
                    var streak=1f-Mathf.SmoothStep(0f,1f,t);
                    go.transform.localScale=new Vector3(normalScale.x*(1f-.35f*streak),normalScale.y*(1f-.35f*streak),normalScale.z*(1f+5f*streak));
                    var color=Color.Lerp(normalColor,Color.white,streak*.85f);color.a=Mathf.Lerp(.05f,1f,Mathf.SmoothStep(0f,1f,t));SetPaintColor(go,color);
                }
            }
            foreach(var uid in shipViews.Keys.Where(uid=>!liveFleetUids.Contains(uid)).ToArray())if(shipViews.Remove(uid,out var gone))Destroy(gone);
        }

        void ApplyWarpVisual(FleetMemberSave member,GameObject go,Vector3 normalScale,Color normalColor)
        {
            var origin=new Vector3(member.WarpOriginX,member.WarpOriginY,member.WarpOriginZ);
            var warpPoint=save.Operation.WarpPointInitialized
                ?new Vector3(save.Operation.WarpPointX,save.Operation.WarpPointY,save.Operation.WarpPointZ)
                :new Vector3(105f,14f,120f);
            SetWarpColliderEnabled(go,false);

            if(member.WarpPhase==FleetWarpPhase.AligningOut)
            {
                var progress=1f-Mathf.Clamp01(member.WarpPhaseSecondsLeft/OperationService.WarpAlignSeconds);
                var acceleration=progress*progress*progress;
                go.transform.position=Vector3.LerpUnclamped(origin,warpPoint,acceleration);
                var direction=warpPoint-go.transform.position;
                if(direction.sqrMagnitude>.0001f)
                {
                    var targetRotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
                    go.transform.rotation=Quaternion.RotateTowards(go.transform.rotation,targetRotation,240f*Time.unscaledDeltaTime*simulationSpeed);
                }
                var streak=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.45f,1f,progress));
                go.transform.localScale=new Vector3(normalScale.x*(1f-.3f*streak),normalScale.y*(1f-.3f*streak),normalScale.z*(1f+4f*streak));
                var flash=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.78f,1f,progress));
                var color=Color.Lerp(normalColor,Color.white,flash*.8f);color.a=Mathf.Lerp(1f,.08f,Mathf.SmoothStep(0f,1f,progress));
                SetPaintColor(go,color);
                return;
            }

            if(member.WarpPhase==FleetWarpPhase.WarpingIn)
            {
                var progress=1f-Mathf.Clamp01(member.WarpPhaseSecondsLeft/OperationService.WarpInSeconds);
                var deceleration=1f-Mathf.Pow(1f-progress,3f);
                go.transform.position=Vector3.LerpUnclamped(warpPoint,origin,deceleration);
                var direction=origin-go.transform.position;
                if(direction.sqrMagnitude>.0001f)go.transform.rotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
                var streak=1f-Mathf.SmoothStep(0f,1f,progress);
                go.transform.localScale=new Vector3(normalScale.x*(1f-.3f*streak),normalScale.y*(1f-.3f*streak),normalScale.z*(1f+4f*streak));
                var color=Color.Lerp(normalColor,Color.white,streak*.7f);color.a=Mathf.Lerp(.08f,1f,Mathf.SmoothStep(0f,1f,progress));
                SetPaintColor(go,color);
                return;
            }

            go.transform.position=origin;go.transform.localScale=normalScale;SetPaintColor(go,normalColor);
        }

        void SyncMiningBeam(FleetMemberSave member,GameObject ship,GameObject asteroid)
        {
            if(!miningBeams.TryGetValue(member.ShipUid,out var beam))
            {
                var go=new GameObject(member.ShipUid+" mining beam");go.transform.SetParent(worldRoot,false);beam=go.AddComponent<LineRenderer>();beam.positionCount=2;beam.useWorldSpace=true;beam.startWidth=.09f;beam.endWidth=.04f;
                var shader=Shader.Find("Sprites/Default")??FindRuntimeShader();
                if(shader!=null)
                {
                    beam.material=new Material(shader);
                    SetMaterialColor(beam.material,MiningBeamBlue);
                }
                else Debug.LogWarning($"EVE Offline: no runtime shader for mining beam {member.ShipUid}.");
                beam.startColor=MiningBeamBlue;beam.endColor=new Color(MiningBeamBlue.r,MiningBeamBlue.g,MiningBeamBlue.b,.25f);miningBeams[member.ShipUid]=beam;
            }
            beam.enabled=member.Order==FleetOrder.Mining;
            if(beam.enabled){beam.SetPosition(0,ship.transform.position+ship.transform.right*.2f);beam.SetPosition(1,asteroid.transform.position);}
        }

        void UpdateTacticalCamera(float dt)
        {
            if(!spaceCamera)return;
            var horizontal=Input.GetAxisRaw("Horizontal");var vertical=Input.GetAxisRaw("Vertical");
            var right=spaceCamera.transform.right;right.y=0;right.Normalize();
            var forward=spaceCamera.transform.forward;forward.y=0;forward.Normalize();
            spaceCamera.transform.position+=(right*horizontal+forward*vertical)*28f*dt;
            var yaw=(Input.GetKey(KeyCode.E)?1f:0f)-(Input.GetKey(KeyCode.Q)?1f:0f);
            if(Math.Abs(yaw)>.01f)spaceCamera.transform.RotateAround(spaceCamera.transform.position,Vector3.up,yaw*45f*dt);
            var wheel=Input.mouseScrollDelta.y;
            if(Math.Abs(wheel)>.01f)spaceCamera.transform.position+=spaceCamera.transform.forward*wheel*6f;
        }

        void RefreshBeltFleetHud()
        {
            if(page!=Page.Belt||!beltFleetPanel)return;
            var members=save.Operation?.Fleet??new List<FleetMemberSave>();
            var liveIds=members.Select(member=>member.ShipUid).Where(uid=>!string.IsNullOrEmpty(uid)).ToHashSet();
            foreach(var uid in beltFleetButtons.Keys.Where(uid=>!liveIds.Contains(uid)).ToArray())
            {
                if(beltFleetButtons[uid])Destroy(beltFleetButtons[uid].gameObject);
                beltFleetButtons.Remove(uid);beltFleetLabels.Remove(uid);
            }
            if(!string.IsNullOrEmpty(selectedShipUid)&&!liveIds.Contains(selectedShipUid))selectedShipUid=string.Empty;

            for(var i=0;i<members.Count;i++)
            {
                var member=members[i];var uid=member.ShipUid;
                if(!beltFleetButtons.TryGetValue(uid,out var button)||!button)
                {
                    var capturedUid=uid;
                    button=Button(beltFleetPanel,string.Empty,new Vector2(10,-74-i*62),new Vector2(-10,60),()=>{selectedShipUid=capturedUid;RefreshBeltHud();},PanelAlt);
                    Pin(button.GetComponent<RectTransform>(),new Vector2(0,1),Vector2.one);
                    beltFleetButtons[uid]=button;beltFleetLabels[uid]=button.GetComponentInChildren<Text>();
                }
                button.GetComponent<RectTransform>().anchoredPosition=new Vector2(10,-74-i*62);
                var selected=uid==selectedShipUid;button.GetComponent<Image>().color=selected?Selection:PanelAlt;
                if(beltFleetLabels.TryGetValue(uid,out var label)&&label)
                {
                    label.color=selected?Background:Ink;
                    label.text=(selected?"▶ ":string.Empty)+FleetHudText(member);
                }
            }
        }

        string FleetHudText(FleetMemberSave member)
        {
            var ship=FindShip(member?.ShipUid);var pilot=FindPilot(member?.PilotId);var hull=Catalog.GetShip(ship?.HullId);
            if(ship==null||hull==null)return $"{pilot?.Name??"Пилот"} • корабль недоступен";
            var hpMax=Math.Max(1f,PreparedPackageService.MaxTotalHp(ship,pilot));var hp=Math.Max(0,ship.ShieldHp+ship.ArmorHp+ship.StructureHp)/hpMax;
            var hold=OperationService.HoldVolume(ship.MiningHold);var capacity=OperationService.MiningHoldCapacity(pilot,hull);
            var status=save.Operation?.BeltWarpActive==true?$"локальный варп {Mathf.CeilToInt(save.Operation.BeltWarpSecondsLeft)}с":member.WarpPhase!=FleetWarpPhase.None?WarpStatus(member):member.Order switch{FleetOrder.Idle=>"ждёт",FleetOrder.Approaching=>ApproachStatus(member,hull),FleetOrder.Mining=>"копает",FleetOrder.UnloadAndReturn=>$"разгрузка {Mathf.CeilToInt(member.TransitSecondsLeft)}с",FleetOrder.DockAndStay=>$"домой {Mathf.CeilToInt(member.TransitSecondsLeft)}с",_=>member.Order.ToString()};
            var cycle=MiningCycleStatus(member,ship,pilot);
            return $"{pilot?.Name??"Пилот"} • {ShipDisplayName(ship)}\n{status} • HP {hp:P0} • трюм {hold:N0}/{capacity:N0} м³"+(string.IsNullOrEmpty(cycle)?string.Empty:"\n"+cycle);
        }

        static string WarpStatus(FleetMemberSave member)
        {
            var seconds=Mathf.CeilToInt(Mathf.Max(0,member.WarpPhaseSecondsLeft));var total=Mathf.CeilToInt(Mathf.Max(0,member.TransitSecondsLeft));
            return member.WarpPhase switch
            {
                FleetWarpPhase.AligningOut=>member.ReturnAfterUnload?$"разгон • возврат {total}с":$"отварп домой • {seconds}с",
                FleetWarpPhase.InTransit=>$"вне белта • возврат {total}с",
                FleetWarpPhase.WarpingIn=>$"выходит из варпа • {total}с",
                _=>member.Order.ToString()
            };
        }

        string ApproachStatus(FleetMemberSave member,ShipDefinition hull)
        {
            var asteroid=save.Operation?.Asteroids.Find(candidate=>candidate.Id==member.TargetAsteroidId);
            if(asteroid==null||hull==null)return "подходит";
            var dx=member.X-asteroid.X;var dy=member.Y-asteroid.Y;var dz=member.Z-asteroid.Z;
            var distanceKm=Mathf.Sqrt(dx*dx+dy*dy+dz*dz)*OperationService.KmPerWorldUnit;
            var remainingKm=Mathf.Max(0,distanceKm-member.PreferredRangeKm);
            if(save.Operation.IndustrialCoreActive&&save.Operation.IndustrialCoreShipUid==member.ShipUid)return $"подходит • {remainingKm:0.0} км • ядро держит";
            var speedKmPerSecond=hull.SpeedMps/1000f;
            if(speedKmPerSecond<=0)return $"подходит • осталось {remainingKm:0.0} км";
            return $"подходит • {remainingKm:0.0} км • ~{Mathf.CeilToInt(remainingKm/speedKmPerSecond)}с";
        }

        string MiningCycleStatus(FleetMemberSave member,ShipSave ship,CharacterSave pilot)
        {
            if(member?.Order!=FleetOrder.Mining||ship==null)return string.Empty;
            var asteroid=save.Operation?.Asteroids.Find(candidate=>candidate.Id==member.TargetAsteroidId);var resource=Catalog.GetOre(asteroid?.OreId);
            if(resource==null)return string.Empty;
            var laserCount=0;var totalYieldM3=0f;var totalRateM3PerSecond=0f;var weightedProgress=0f;var minCycle=float.MaxValue;var maxCycle=0f;
            foreach(var fitted in ship.Modules.Where(candidate=>OperationService.CanMineResource(candidate,resource)).OrderBy(candidate=>candidate.Slot))
            {
                var total=OperationService.MiningCycleSecondsForSlot(save,member,fitted.Slot);if(total<=0)continue;
                var elapsed=Mathf.Clamp(member.MiningCycles?.Find(candidate=>candidate.Slot==fitted.Slot)?.ProgressSeconds??0,0,total);
                var yieldM3=OperationService.MiningYieldM3ForSlot(save,member,fitted.Slot);if(yieldM3<=0)continue;
                laserCount++;totalYieldM3+=yieldM3;totalRateM3PerSecond+=yieldM3/total;weightedProgress+=yieldM3*(elapsed/total);minCycle=Mathf.Min(minCycle,total);maxCycle=Mathf.Max(maxCycle,total);
            }
            var parts=new List<string>();
            if(laserCount>0&&totalRateM3PerSecond>0)
            {
                var combinedCycle=totalYieldM3/totalRateM3PerSecond;var progress01=totalYieldM3>0?Mathf.Clamp01(weightedProgress/totalYieldM3):0;var cyclePrefix=maxCycle-minCycle>.01f?"≈":string.Empty;
                parts.Add($"Комплекс ×{laserCount} • цикл {cyclePrefix}{Mathf.FloorToInt(progress01*combinedCycle)}/{Mathf.CeilToInt(combinedCycle)}с • {totalYieldM3:N1}м³/ц • {totalRateM3PerSecond:N2}м³/с");
            }
            var drone=Catalog.GetDrone(ship.MiningDroneId);
            var hasMiningDrones=resource.Kind==ResourceKind.Ore&&drone?.Mining==true&&ship.MiningDroneCount>0&&SkillService.GetLevel(pilot,"drones")>0;
            if(hasMiningDrones)
            {
                if(save.Operation.Enemies.Count>0)parts.Add("Дроны: пауза (NPC)");
                else {var droneYield=OperationService.MiningDroneYieldM3PerCycle(save,member);parts.Add($"Дроны • {Mathf.FloorToInt(member.DroneCycleProgressSeconds)}/60с • {droneYield:N1}м³/ц • {droneYield/60f:N2}м³/с");}
            }
            return string.Join("\n",parts);
        }

        void RefreshSelectedAsteroidInfo()
        {
            if(!selectedAsteroidInfoText)return;
            var asteroid=save.Operation?.Asteroids.Find(candidate=>candidate.Id==selectedAsteroidId&&candidate.RemainingUnits>0);
            var gasSite=save.Operation?.Asteroids.Any(candidate=>Catalog.GetOre(candidate.OreId)?.Kind==ResourceKind.Gas)==true;
            var iceSite=save.Operation?.Asteroids.Any(candidate=>Catalog.GetOre(candidate.OreId)?.Kind==ResourceKind.Ice)==true;
            if(asteroid!=null)selectedAsteroidInfoText.text=AsteroidInfo(asteroid);
            else if(gasSite)selectedAsteroidInfoText.text="Газовое облако не выбрано.\nКликни по облаку в аномалии.";
            else if(iceSite)selectedAsteroidInfoText.text="Ледяная глыба не выбрана.\nКликни по глыбе в аномалии.";
            else selectedAsteroidInfoText.text="Астероид не выбран.\nКликни по камню в белте.";
        }

        static string AsteroidInfo(AsteroidSave asteroid)
        {
            var ore=Catalog.GetOre(asteroid?.OreId);
            if(asteroid==null||ore==null)return "Цель добычи недоступна.";
            var kind=ore.Kind==ResourceKind.Gas?"ГАЗОВОЕ ОБЛАКО":ore.Kind==ResourceKind.Ice?"ЛЕДЯНАЯ ГЛЫБА":"АСТЕРОИД";
            var grade=ore.Kind==ResourceKind.Ore?$" • ГРЕЙД {OreGradeName(ore)}":string.Empty;
            var value=ore.Kind==ResourceKind.Ore?$" • качество ×{ore.ValueMultiplier:0.00}":string.Empty;
            return $"{kind} • {ore.DisplayName}{grade}\n{asteroid.RemainingUnits:N0} ед. • {OperationService.AsteroidVolumeM3(asteroid):N1} м³\n{ore.UnitVolumeM3:N2} м³/ед.{value}";
        }

        static string OreGradeValueText(OreDefinition ore)
        {
            if(ore?.Kind!=ResourceKind.Ore)return string.Empty;
            return $"Грейд {OreGradeName(ore)} • качество ×{ore.ValueMultiplier:0.00}";
        }

        static string OreGradeName(OreDefinition ore)
        {
            var grade=Catalog.GetOreGrade(ore);
            return grade==0?"0":SkillService.ToRoman(grade);
        }

        void RefreshBeltHud()
        {
            if(string.IsNullOrEmpty(selectedShipUid))return;
            var member=save.Operation?.Fleet.Find(candidate=>candidate.ShipUid==selectedShipUid);var ship=FindShip(selectedShipUid);
            if(member==null||ship==null){selectedShipUid=string.Empty;Tell("Корабль уже недоступен.");RefreshBeltFleetHud();return;}
            Tell(FleetHudText(member).Replace('\n',' '));RefreshBeltFleetHud();
        }

        void TryIssueMiningOrder()
        {
            if(string.IsNullOrEmpty(selectedShipUid)||string.IsNullOrEmpty(selectedAsteroidId)){Tell("Выбери корабль и цель добычи.");return;}
            var member=save.Operation?.Fleet.Find(candidate=>candidate.ShipUid==selectedShipUid);var ship=FindShip(selectedShipUid);
            if(member==null||ship==null||ship.Location!=ShipLocation.Belt){selectedShipUid=string.Empty;Tell("Выбранный корабль уже не находится в зоне добычи.");RefreshBeltFleetHud();return;}
            var asteroid=save.Operation.Asteroids.Find(candidate=>candidate.Id==selectedAsteroidId&&candidate.RemainingUnits>0);
            if(asteroid==null){selectedAsteroidId=string.Empty;RefreshSelectedAsteroidInfo();Tell("Эта цель уже истощена. Выбери другую.");return;}
            var pilot=FindPilot(member.PilotId);
            var hasMiner=ship.Modules.Any(fitted=>Catalog.GetModule(fitted.ModuleId)?.Kind is ModuleKind.MiningLaser or ModuleKind.StripMiner or ModuleKind.IceMiningLaser or ModuleKind.IceHarvester or ModuleKind.GasCloudScoop or ModuleKind.GasCloudHarvester);
            var miningDrone=Catalog.GetDrone(ship.MiningDroneId);
            var hasMiningDrones=miningDrone?.Mining==true&&ship.MiningDroneCount>0&&SkillService.GetLevel(pilot,"drones")>0;
            if(!hasMiner&&!hasMiningDrones){Tell("На корабле нет рабочего добывающего модуля или mining drones.");return;}
            if(OperationService.AssignTarget(save,selectedShipUid,selectedAsteroidId))Tell("Корабль подойдёт на рабочую дальность и начнёт цикл.");
            else Tell("Приказ не принят: рудные, ледовые и газовые добывающие модули работают только со своим ресурсом.");
            RefreshBeltFleetHud();
        }

        void BuyPackage(PreparedMiningPackage package)
        {
            var pilot=FindPilot(selectedPilotId);
            var assignImmediately=pilot!=null&&string.IsNullOrEmpty(pilot.AssignedShipUid)&&PreparedPackageService.CanUsePackage(pilot,package);
            string message;
            var bought=assignImmediately
                ?PreparedPackageService.TryBuy(save,pilot.Id,package?.Id,out _,out message)
                :PreparedPackageService.TryBuyToHangar(save,package?.Id,out _,out message);
            if(bought)SaveService.Save(save);
            Tell(message);BuildStationAgain();
        }
        void AssignPackageToAll(PreparedMiningPackage package)
        {
            if(package==null){Tell("Готовый комплект не найден.");return;}
            var applied=PreparedPackageService.TryAssignAll(save,package.Id,out var result);
            if(applied&&result.AssignedCount>0)SaveService.Save(save);
            Tell(result?.Message??"Не удалось подготовить массовую посадку.");BuildStationAgain();
        }
        void BuyItem(string id,double unitPrice,int amount=1){var price=unitPrice*amount;if(price<=0||save.Isk<price){Tell("Не хватает ISK или цена недоступна.");return;}save.Isk-=price;OperationService.AddItem(save.StationInventory,id,amount);Tell($"Куплено: {ItemName(id)} × {amount}.");SaveService.Save(save);BuildStationAgain();}
        void SellOre(InventoryStack stack){var ore=ResourceForItem(stack.ItemId);if(ore==null)return;var income=stack.Quantity*MarketService.OreBuyPerUnit(save,ore);save.Isk+=income;Tell($"Продано {stack.Quantity:N0} {ItemName(stack.ItemId)}: +{income:N0} ISK.");stack.Quantity=0;SaveService.Save(save);BuildStationAgain();}

        void CommandReturnShip(string shipUid,bool returnAfterUnload)
        {
            if(string.IsNullOrWhiteSpace(shipUid)){Tell("Сначала выбери корабль.");return;}
            var result=OperationService.ReturnShipWithResult(save,shipUid,returnAfterUnload);
            if(result.Success)SaveService.Save(save);
            Tell(result.Summary);
            if(page==Page.Belt)RefreshBeltFleetHud();
        }

        void CommandReturnFleet(bool returnAfterUnload)
        {
            var result=OperationService.ReturnFleetWithResult(save,returnAfterUnload);
            if(result.Success)SaveService.Save(save);
            Tell(result.RequestedCount==0?"В активном флоте нет кораблей.":$"Команда флоту: сразу {result.ImmediateCount}, после Industrial Core {result.QueuedCount}, отказов {result.FailedCount}.");
            if(page==Page.Belt)RefreshBeltFleetHud();
        }
        void TransferFuel(ShipSave ship,double requested,bool toShip){var pilot=FindPilot(selectedPilotId);FinishService(FittingService.TryTransferFuel(save,pilot.Id,ship.Uid,requested,toShip,out _,out var msg),msg);}
        void UnloadAllCargo(ShipSave ship)
        {
            var pilot=FindPilot(selectedPilotId);var itemIds=ship?.CargoHold?.Where(stack=>stack.Quantity>.0001).Select(stack=>stack.ItemId).Distinct().ToArray()??Array.Empty<string>();
            if(itemIds.Length==0){Tell("Cargo уже пуст.");return;}
            var moved=0d;var failed=new List<string>();
            foreach(var itemId in itemIds)
            {
                if(FittingService.TryTransferCargo(save,pilot.Id,ship.Uid,itemId,double.PositiveInfinity,false,out var transferred,out var message))moved+=transferred;
                else failed.Add($"{ItemName(itemId)}: {message}");
            }
            if(moved>0)SaveService.Save(save);
            Tell(failed.Count==0?$"Весь cargo выгружен на общий склад: {moved:N0} ед.":$"Выгружено {moved:N0} ед.; не удалось: {string.Join(" • ",failed)}");BuildStationAgain();
        }
        void RepairShip(CharacterSave pilot,ShipSave ship){FinishService(FittingService.TryRepairShip(save,pilot.Id,ship.Uid,out _,out var msg),msg);}
        void FinishService(bool success,string message){Tell(message);if(success)SaveService.Save(save);BuildStationAgain();}

        void BuildHangar(RectTransform panel,CharacterSave pilot,float y)
        {
            const int pageSize=8;
            var allShips=save.Ships.Where(candidate=>candidate.Location==ShipLocation.Station&&save.Characters.All(character=>character.AssignedShipUid!=candidate.Uid)).ToList();
            if(allShips.Count==0){hangarPage=0;Text(panel,"ОБЩИЙ АНГАР",11,Cyan,new Vector2(18,y),new Vector2(-18,22),TextAnchor.MiddleLeft,FontStyle.Bold);Text(panel,"Свободных комплектов нет.",11,Muted,new Vector2(18,y-30),new Vector2(-18,24),TextAnchor.MiddleLeft);return;}
            var pageCount=Math.Max(1,(allShips.Count+pageSize-1)/pageSize);hangarPage=Math.Clamp(hangarPage,0,pageCount-1);
            Text(panel,$"ОБЩИЙ АНГАР • СТРАНИЦА {hangarPage+1}/{pageCount}",11,Cyan,new Vector2(18,y),new Vector2(-18,22),TextAnchor.MiddleLeft,FontStyle.Bold);
            var ships=allShips.Skip(hangarPage*pageSize).Take(pageSize).ToList();
            for(var i=0;i<ships.Count;i++){var hangarShip=ships[i];var package=Catalog.GetPackage(hangarShip.PackageId);var uid=hangarShip.Uid;var resourcePackage=package!=null&&(package.Role==PreparedPackageRole.Ice||package.Role==PreparedPackageRole.Gas);WideButton(panel,$"{ShipDisplayName(hangarShip)}  •  НАЗНАЧИТЬ",y-34-i*42,()=>AssignShip(pilot,uid),resourcePackage?Cyan:Border);}
            if(pageCount>1)WideButton(panel,"СЛЕДУЮЩАЯ СТРАНИЦА АНГАРА →",y-38-ships.Count*42,()=>{hangarPage=(hangarPage+1)%pageCount;BuildStationAgain();},Cyan);
        }

        void UnassignShip(CharacterSave pilot,ShipSave ship)
        {
            if(pilot==null||ship==null||ship.Location!=ShipLocation.Station)return;
            RescaleShieldForPilot(ship,pilot,null);
            pilot.AssignedShipUid=string.Empty;
            SaveService.Save(save);Tell($"{ShipDisplayName(ship)} помещён в общий ангар.");BuildStationAgain();
        }

        void AssignShip(CharacterSave pilot,string uid)
        {
            var ship=FindShip(uid);
            if(pilot==null||ship==null||!string.IsNullOrEmpty(pilot.AssignedShipUid)){Tell("Сначала освободи слот корабля этого пилота.");return;}
            if(!OperationService.CanFly(pilot,ship)){Tell("Пилоту не хватает навыков корабля или полного готового комплекта.");return;}
            RescaleShieldForPilot(ship,null,pilot);
            pilot.AssignedShipUid=uid;pilot.DeployOnLaunch=true;
            SaveService.Save(save);Tell($"{ShipDisplayName(ship)} назначен {pilot.Name}.");BuildStationAgain();
        }

        static void RescaleShieldForPilot(ShipSave ship,CharacterSave oldPilot,CharacterSave newPilot)
        {
            var oldMaximum=Math.Max(1f,PreparedPackageService.MaxShieldHp(ship,oldPilot));
            var healthFraction=Math.Clamp(ship.ShieldHp/oldMaximum,0f,1f);
            ship.ShieldHp=PreparedPackageService.MaxShieldHp(ship,newPilot)*healthFraction;
        }

        void BeginTravel(string locationId){if(TravelService.TryStart(save,locationId,out var message)){SaveService.Save(save);Tell(message);Open(Page.Fleet);}else Tell(message);}
        void BeginSameSystemBeltWarp(string locationId){if(TravelService.TryStartSameSystemBeltWarp(save,locationId,out var message)){SaveService.Save(save);Tell(message);if(page!=Page.Belt)BuildStationAgain();}else Tell(message);}
        void BuildStationAgain(){Open(page);}
        string StationStateSignature()
        {
            var operation=save.Operation;
            return string.Join("|",save.Characters.Select(pilot=>$"{pilot.Id}:{pilot.AssignedShipUid}")) + "#" +
                   string.Join("|",save.Ships.OrderBy(ship=>ship.Uid).Select(ship=>$"{ship.Uid}:{(int)ship.Location}")) + "#" +
                   $"{operation.Active}:{operation.LocationId}:{operation.TravelActive}:{operation.TravelDestinationLocationId}:{operation.TravelIsAutomatic}:{operation.BeltWarpActive}:{operation.BeltWarpDestinationLocationId}:{operation.BeltWarpIsAutomatic}:{operation.AutoNextBelt}:{operation.AutoSecurityFloorInitialized}:{operation.AutoSecurityFloorTenths}:{operation.Fleet?.Count??0}";
        }

        void UpdateHeader()
        {
            if(walletText)walletText.text=$"{save.Isk:N0} ISK";
            if(operationText)
            {
                if(save.Operation.BeltWarpActive)
                {
                    var source=Catalog.GetLocation(save.Operation.LocationId);
                    var destination=Catalog.GetLocation(save.Operation.BeltWarpDestinationLocationId);
                    operationText.text=$"{BeltWarpModeLabel(save.Operation,true)}: {source?.SystemName??"?"} → {destination?.SystemName??"?"} • {Mathf.CeilToInt(save.Operation.BeltWarpSecondsLeft)}с";
                }
                else if(save.Operation.TravelActive)
                {
                    var source=Catalog.GetLocation(save.Operation.TravelSourceLocationId);
                    var destination=Catalog.GetLocation(save.Operation.TravelDestinationLocationId);
                    var pending=!save.Operation.TravelIsAutomatic&&destination!=null?$" • затем SEC≥{SecurityText(MiningSiteService.SecurityTierTenths(destination))}":string.Empty;
                    operationText.text=$"{(save.Operation.TravelIsAutomatic?"АВТО":"ВРУЧНУЮ")}: {source?.SystemName??"Jita"} → {destination?.SystemName??"?"} • {Mathf.CeilToInt(save.Operation.TravelSecondsLeft)}с{pending}";
                }
                else if(save.Operation.Active)
                {
                    operationText.text=$"ОПЕРАЦИЯ: {Catalog.GetLocation(save.Operation.LocationId)?.DisplayName} • {save.Operation.Fleet.Count}/{Catalog.FleetCapacity} • NPC {save.Operation.Enemies.Count} • SEC≥{SecurityText(AutoSecurityFloorTenths())}";
                }
                else operationText.text=$"ОПЕРАЦИЯ НЕ АКТИВНА • SEC≥{SecurityText(AutoSecurityFloorTenths())}";
            }
            if(noticeText)noticeText.text=noticeSeconds>0?notice:"Автосохранение активно";
        }
        void Tell(string message){if(string.IsNullOrWhiteSpace(message))return;notice=message;noticeSeconds=7f;}
        CharacterSave FindPilot(string id)=>save.Characters.Find(x=>x.Id==id);
        ShipSave FindShip(string uid)=>string.IsNullOrEmpty(uid)?null:save.Ships.Find(x=>x.Uid==uid);
        string ShipDisplayName(ShipSave ship)
        {
            if(ship==null)return "—";var displayName=PreparedPackageService.DisplayName(ship);
            return Catalog.GetPackage(ship.PackageId)!=null?displayName:$"{displayName} • LEGACY CUSTOM";
        }
        static string PackageRoleLabel(PreparedPackageRole role)=>role switch{PreparedPackageRole.Ore=>"РУДА",PreparedPackageRole.Ice=>"ЛЁД",PreparedPackageRole.Gas=>"ГАЗ",PreparedPackageRole.Mercoxit=>"РУДА",PreparedPackageRole.Booster=>"БУСТЕР",_=>role.ToString().ToUpperInvariant()};
        string LocationResourceSummary(LocationDefinition location)
        {
            var resources=(location?.OreIds??Array.Empty<string>()).Select(Catalog.GetOre).Where(resource=>resource!=null).ToList();
            if(resources.Count==0)return "РЕСУРСЫ: —";
            var kinds=resources.Select(resource=>resource.Kind).Distinct().ToArray();
            var kind=kinds.Length==1?kinds[0] switch{ResourceKind.Ice=>"ЛЁД",ResourceKind.Gas=>"ГАЗ",_=>"РУДА"}:"РЕСУРСЫ";
            var names=resources.Select(resource=>resource.DisplayName).Distinct().ToArray();
            var shown=names.Length<=2?string.Join(", ",names):$"{string.Join(", ",names.Take(2))} +{names.Length-2}";
            return $"{kind}: {shown}";
        }
        string LocationCompatibilitySummary(LocationDefinition location)
        {
            var selected=save.Characters.Where(pilot=>pilot.DeployOnLaunch&&!string.IsNullOrWhiteSpace(pilot.AssignedShipUid)).ToList();
            if(selected.Count==0)return "выбрано 0 кораблей";
            var compatible=selected.Count(pilot=>
            {
                var ship=FindShip(pilot.AssignedShipUid);var hull=Catalog.GetShip(ship?.HullId);
                return ship!=null&&OperationService.CanFly(pilot,ship)&&OperationService.LocationSupportsShip(location,ship,hull);
            });
            return $"подходит {compatible}/{selected.Count}";
        }
        static string BurstProfileLabel(string profileId)
        {
            var id=(profileId??string.Empty).ToLowerInvariant();
            if(id.Contains("field-enhancement"))return "дальность добывающих лазеров";
            if(id.Contains("optimization"))return "скорость добывающего цикла";
            if(id.Contains("efficiency"))return "mining critical и снижение отходов";
            if(id.Contains("preservation"))return "legacy: не используется";
            return "универсальное усиление";
        }
        static string PackageGradeLabel(PreparedMiningPackage package)
        {
            if(package==null)return "НЕИЗВЕСТНЫЙ КОМПЛЕКТ";
            if(package.Grade==PreparedPackageGrade.T0)return "T0 • БЕСПЛАТНЫЙ СТАРТ";
            if(package.ImplicitUniversalTypeALevel>0)return $"T2 • УНИВЕРСАЛЬНЫЙ TYPE A {SkillService.ToRoman(package.ImplicitUniversalTypeALevel)}";
            return package.Grade switch{PreparedPackageGrade.T1=>"T1 • БАЗОВЫЙ ПОЛНЫЙ",PreparedPackageGrade.T2=>"T2 • ПОЛНЫЙ",PreparedPackageGrade.ORE=>"ORE • ФРАКЦИОННЫЙ",_=>package.Grade.ToString().ToUpperInvariant()};
        }
        string LockedPackageSummary(ShipSave ship)
        {
            var package=Catalog.GetPackage(ship?.PackageId);
            if(package!=null)return $"{PreparedPackageService.EquipmentSummary(package)}\nРоль: {PackageRoleLabel(package.Role)} • Grade {package.Grade} • состав нельзя менять вручную.";
            var hull=Catalog.GetShip(ship?.HullId);var moduleNames=(ship?.Modules??new List<FittedModuleSave>()).Select(module=>Catalog.GetModule(module.ModuleId)?.DisplayName??module.ModuleId).Where(name=>!string.IsNullOrWhiteSpace(name)).Distinct().ToArray();
            var equipment=moduleNames.Length==0?"сохранённый пользовательский состав без распознанных модулей":string.Join(" • ",moduleNames);
            return $"LEGACY CUSTOM • {hull?.DisplayName??ship?.HullId}\n{equipment}\nКорабль сохранён и доступен, но его старый состав не переоснащается на этом экране.";
        }
        static OreDefinition ResourceForItem(string itemId)
        {
            var raw=Catalog.GetOre(itemId);if(raw!=null)return raw;
            return Catalog.TryGetCompressedSource(itemId,out var source)?source:null;
        }

        string ItemName(string id)
        {
            if(string.IsNullOrEmpty(id))return "нет";
            if(Catalog.TryGetCompressedSource(id,out var source))return $"Сжатый {source.DisplayName}";
            return Catalog.GetOre(id)?.DisplayName??Catalog.GetModule(id)?.DisplayName??Catalog.GetDrone(id)?.DisplayName??Catalog.GetCrystal(id)?.DisplayName??Catalog.GetBurstCharge(id)?.DisplayName??Catalog.GetShip(id)?.DisplayName??(id=="heavy-water"?"Heavy Water":id);
        }
        static string FormatDuration(double seconds){var t=TimeSpan.FromSeconds(Math.Max(0,seconds));return t.TotalDays>=1?$"{t.TotalDays:0.0} д":t.TotalHours>=1?$"{t.TotalHours:0.0} ч":$"{t.TotalMinutes:0} мин";}
        static string FormatCountdown(float seconds){seconds=Math.Max(0,seconds);var whole=Mathf.CeilToInt(seconds);return whole>=3600?$"{whole/3600:00}:{whole%3600/60:00}:{whole%60:00}":$"{whole/60:00}:{whole%60:00}";}
        static string FormatUtc(long unix){return unix<=0?"ETA неизвестно":DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("dd.MM HH:mm 'UTC'",CultureInfo.InvariantCulture);}
        static string FormatIsk(double value){value=Math.Max(0,value);return value>=1_000_000_000?$"{value/1_000_000_000:0.##} млрд ISK":value>=1_000_000?$"{value/1_000_000:0.##} млн ISK":value>=1_000?$"{value/1_000:0.##} тыс. ISK":$"{value:N0} ISK";}

        void Title(Transform parent,string title,string subtitle){Text(parent,title,24,Ink,new Vector2(24,-18),new Vector2(700,38),TextAnchor.MiddleLeft,FontStyle.Bold);Text(parent,subtitle,12,Muted,new Vector2(25,-56),new Vector2(1000,24),TextAnchor.MiddleLeft);}
        void Nav(Transform parent,string label,float y,Page target){var b=Button(parent,label,new Vector2(14,-y),new Vector2(-14,38),()=>Open(target),page==target?Cyan:PanelAlt);Pin(b.GetComponent<RectTransform>(),new Vector2(0,1),Vector2.one);}
        Button WideButton(Transform parent,string label,float y,Action action,Color color,bool top=true,float height=40f){var b=Button(parent,label,new Vector2(12,y),new Vector2(-12,height),action,color);Pin(b.GetComponent<RectTransform>(),top?new Vector2(0,1):Vector2.zero,top?Vector2.one:new Vector2(1,0));return b;}
        RectTransform Row(Transform parent,int index,float height){var top=-index*(height+4);return Rect("Row",parent,new Vector2(0,top-height),new Vector2(0,top),new Vector2(0,1),Vector2.one,PanelAlt);}
        (RectTransform viewport,RectTransform content) Scroll(Transform parent)=>ScrollArea(parent,Vector2.zero,Vector2.zero);
        (RectTransform viewport,RectTransform content) ScrollArea(Transform parent,Vector2 min,Vector2 max){var go=new GameObject("Scroll",typeof(Image),typeof(ScrollRect));go.transform.SetParent(parent,false);go.GetComponent<Image>().color=Color.clear;var rt=go.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=min;rt.offsetMax=max;var vp=new GameObject("Viewport",typeof(Image),typeof(RectMask2D));vp.transform.SetParent(go.transform,false);vp.GetComponent<Image>().color=Color.clear;var vrt=vp.GetComponent<RectTransform>();vrt.anchorMin=Vector2.zero;vrt.anchorMax=Vector2.one;vrt.offsetMin=vrt.offsetMax=Vector2.zero;var c=new GameObject("Content",typeof(RectTransform));c.transform.SetParent(vp.transform,false);var crt=c.GetComponent<RectTransform>();crt.anchorMin=new Vector2(0,1);crt.anchorMax=Vector2.one;crt.pivot=new Vector2(.5f,1);crt.offsetMin=crt.offsetMax=Vector2.zero;var scroll=go.GetComponent<ScrollRect>();scroll.viewport=vrt;scroll.content=crt;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=22;return(vrt,crt);}
        RectTransform Rect(string name,Transform parent,Vector2 offsetMin,Vector2 offsetMax,Vector2 anchorMin,Vector2 anchorMax,Color color){var go=new GameObject(name,typeof(Image));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.anchorMin=anchorMin;rt.anchorMax=anchorMax;rt.offsetMin=offsetMin;rt.offsetMax=offsetMax;go.GetComponent<Image>().color=color;return rt;}
        Text Text(Transform parent,string value,int size,Color color,Vector2 pos,Vector2 dimensions,TextAnchor alignment,FontStyle style=FontStyle.Normal){var go=new GameObject("Text",typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.alignment=alignment;t.fontStyle=style;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;var rt=t.rectTransform;rt.pivot=new Vector2(0,1);if(dimensions.x<0){rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.offsetMin=new Vector2(pos.x,pos.y-dimensions.y);rt.offsetMax=new Vector2(dimensions.x,pos.y);}else{rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.anchoredPosition=pos;rt.sizeDelta=dimensions;}return t;}
        Button Button(Transform parent,string label,Vector2 pos,Vector2 dimensions,Action action,Color color){var go=new GameObject(label,typeof(Image),typeof(Button));go.transform.SetParent(parent,false);go.GetComponent<Image>().color=color;var b=go.GetComponent<Button>();var colors=b.colors;colors.highlightedColor=Color.Lerp(color,Color.white,.15f);colors.pressedColor=Color.Lerp(color,Color.black,.2f);b.colors=colors;b.onClick.AddListener(()=>action());var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.anchoredPosition=pos;rt.sizeDelta=dimensions;var text=Text(go.transform,label,10,color==Cyan||color==Amber?Background:Ink,Vector2.zero,Vector2.zero,TextAnchor.MiddleCenter,FontStyle.Bold);text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;return b;}
        void BuildStars(Transform parent){for(var i=0;i<42;i++){var go=new GameObject("Star",typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.color=new Color(1,1,1,i%5==0?.25f:.08f);image.raycastTarget=false;var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2((i*37%101)/100f,(i*67%97)/100f);rt.sizeDelta=Vector2.one*(i%6==0?3:1);}}
        static void Pin(RectTransform rt,Vector2 min,Vector2 max){var pos=rt.anchoredPosition;var size=rt.sizeDelta;rt.anchorMin=min;rt.anchorMax=max;rt.anchoredPosition=pos;rt.sizeDelta=size;}
        static void Paint(GameObject go,Color color)
        {
            var renderer=go?.GetComponent<Renderer>();if(renderer==null)return;
            var shader=FindRuntimeShader();
            Material material=null;
            if(shader!=null)material=new Material(shader);
            else if(renderer.sharedMaterial!=null)material=new Material(renderer.sharedMaterial);
            if(material==null){Debug.LogWarning($"EVE Offline: no runtime shader or primitive material for {go.name}.");return;}
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            material.color=color;
            renderer.material=material;
        }
        static void SetPaintColor(GameObject go,Color color)
        {
            var material=go?.GetComponent<Renderer>()?.sharedMaterial;if(material==null)return;SetMaterialColor(material,color);
        }
        static void ConfigureWarpTransparency(GameObject go)
        {
            var renderer=go?.GetComponent<Renderer>();var material=renderer?.sharedMaterial;if(material==null)return;
            material.SetOverrideTag("RenderType","Transparent");
            if(material.HasProperty("_Surface"))material.SetFloat("_Surface",1f);
            if(material.HasProperty("_Mode"))material.SetFloat("_Mode",3f);
            if(material.HasProperty("_Blend"))material.SetFloat("_Blend",0f);
            if(material.HasProperty("_SrcBlend"))material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            if(material.HasProperty("_DstBlend"))material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            if(material.HasProperty("_ZWrite"))material.SetFloat("_ZWrite",0f);
            material.DisableKeyword("_ALPHATEST_ON");material.EnableKeyword("_ALPHABLEND_ON");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue=(int)RenderQueue.Transparent;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        static void SetWarpColliderEnabled(GameObject go,bool enabled)
        {
            var collider=go?.GetComponent<Collider>();if(collider)collider.enabled=enabled;
        }
        static void SetMaterialColor(Material material,Color color)
        {
            if(material==null)return;
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            material.color=color;
        }
        static Shader FindRuntimeShader()
        {
            foreach(var shaderName in new[]{"Universal Render Pipeline/Lit","Standard","Legacy Shaders/Diffuse","Sprites/Default","UI/Default"})
            {
                var shader=Shader.Find(shaderName);if(shader!=null)return shader;
            }
            return null;
        }
        static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var color);return color;}
    }

    public sealed class WorldClickable : MonoBehaviour
    {
        public Action Clicked;
        void OnMouseDown()=>Clicked?.Invoke();
    }
}
