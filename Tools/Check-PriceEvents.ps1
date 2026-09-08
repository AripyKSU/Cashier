$ErrorActionPreference = 'Stop'
$result = @'
var events = Util.ParseFromCSV<PriceEventData>(File.ReadAllText("Assets/Datas/PriceEventData.csv")).ToDictionary(x => x.Idx);
var schedules = Util.ParseFromCSV<PriceEventScheduleData>(File.ReadAllText("Assets/Datas/PriceEventScheduleData.csv")).ToDictionary(x => x.Idx);
var products = Util.ParseFromCSV<ProductData>(File.ReadAllText("Assets/Datas/Customer/ProductData.csv")).ToDictionary(x => x.Idx);
var textRows = Util.ParseFromCSV<TextData>(File.ReadAllText("Assets/Datas/TextData.csv")).ToDictionary(x => x.Idx);
var eventTable = new PriceEventDataTable(); eventTable.LoadData(File.ReadAllText("Assets/Datas/PriceEventData.csv"));
var scheduleTable = new PriceEventScheduleDataTable(); scheduleTable.LoadData(File.ReadAllText("Assets/Datas/PriceEventScheduleData.csv"));
foreach (var row in events.Values) {
    row.Validate();
    if (!textRows.ContainsKey(row.NameIdx) || !textRows.ContainsKey(row.DescriptionIdx)) throw new Exception("Text FK");
}
foreach (var row in schedules.Values) row.Validate();
var newspaper = schedules[10001];
if (!newspaper.IsDue(0) || newspaper.IsDue(1) || !newspaper.IsDue(2)) throw new Exception("Periodic dates");
var bounded = new PriceEventScheduleData { Idx=1, EventIdx=9001, ChannelValue=1, StartDay=2, EndDay=4, RepeatDays=0, SelectionWeight=1 };
if (bounded.IsDue(1) || !bounded.IsDue(2) || bounded.IsDue(4) || bounded.IsDue(5)) throw new Exception("One shot dates");
bounded.ChannelValue=2;
if (!bounded.IsDue(4) || bounded.IsDue(5)) throw new Exception("End date inclusive");
var seen = new HashSet<string>(); int radioCount=0;
for (int i=0;i<1000;i++) {
    var scheduler = new PriceEventScheduler(new System.Random(i));
    var state = scheduler.CreateDay((uint)(i%2), events, schedules, products);
    seen.Add(state.NewspaperEventIdx.HasValue + "/" + state.RadioEventIdx.HasValue);
    if (state.RadioEventIdx.HasValue) radioCount++;
    float delay = scheduler.GetRadioDelaySeconds();
    if(delay < 0 || delay > 60 || state.IsRadioBroadcast) throw new Exception("Broadcast timing/initial state");
}
if (seen.Count!=2 || radioCount!=1000) throw new Exception("Radio candidate selection failed");
var newspaperOnly = schedules.Where(x => x.Value.Channel == PriceEventChannel.Newspaper).ToDictionary(x => x.Key, x => x.Value);
if (new PriceEventScheduler(new System.Random(1)).CreateDay(0,events,newspaperOnly,products).RadioEventIdx.HasValue) throw new Exception("Radio without candidates");
var one = new Dictionary<uint, ProductData> { [1001] = new ProductData { Idx=1001, NameIdx=8012, ProductType=ProductType.Water, BasePrice=101, IsAvailable=true } };
var effect = new PriceEventData { Idx=9001, NameIdx=8042, DescriptionIdx=8043, ProductIdxs=new uint[]{1001}, ProductTypes=new[]{ProductType.Water}, ChangeTypeValue=1, ChangeValue=-200 };
var fx = new Dictionary<uint, PriceEventData>{ [9001]=effect };
var rows = new Dictionary<uint, PriceEventScheduleData>{ [10001]=new PriceEventScheduleData{Idx=10001, EventIdx=9001, ChannelValue=1, StartDay=0, SelectionWeight=1} };
var engine = new PriceEventScheduler(new System.Random(1));
var today=engine.CreateDay(0,fx,rows,one);
if (today.Prices[1001]!=80 || one[1001].BasePrice!=101 || engine.CreateDay(1,fx,rows,one).Prices[1001]!=101) throw new Exception("Floor, union, reset or source mutation");
rows.Add(10002,new PriceEventScheduleData{Idx=10002,EventIdx=9001,ChannelValue=2,StartDay=0,SelectionWeight=1});
for(int i=0;i<20;i++) if(engine.ApplyRadio(new PriceEventScheduler(new System.Random(i)).CreateDay(0,fx,rows,one),fx,one).Prices[1001]!=80) throw new Exception("Same event applied twice");
rows.Remove(10001);
var beforeRadio=engine.CreateDay(0,fx,rows,one);
var afterRadio=engine.ApplyRadio(beforeRadio,fx,one);
if(beforeRadio.Prices[1001]!=101 || afterRadio.Prices[1001]!=80 || !afterRadio.IsRadioBroadcast || !object.ReferenceEquals(afterRadio,engine.ApplyRadio(afterRadio,fx,one))) throw new Exception("Delayed immutable prices or repeated broadcast");
rows.Add(10001,new PriceEventScheduleData{Idx=10001,EventIdx=9001,ChannelValue=1,StartDay=0,SelectionWeight=1});
rows.Remove(10002);
effect.ChangeTypeValue=2;effect.ChangeValue=-1000;
if(engine.CreateDay(0,fx,rows,one).Prices[1001]!=1) throw new Exception("Price floor");
effect.ChangeTypeValue=0;effect.ChangeValue=0;effect.ProductIdxs=Array.Empty<uint>();effect.ProductTypes=Array.Empty<ProductType>();
var neutral=engine.CreateDay(0,fx,rows,one);
if(!neutral.NewspaperEventIdx.HasValue || neutral.Prices[1001]!=101) throw new Exception("Neutral news");
effect.ChangeTypeValue=1;effect.ChangeValue=int.MaxValue;effect.ProductIdxs=new uint[]{1001};
one[1001].BasePrice=uint.MaxValue;bool overflow=false;
try { engine.CreateDay(0,fx,rows,one); } catch(OverflowException){overflow=true;}
if(!overflow) throw new Exception("Price overflow");
return "PRICE_EVENTS_PASS: CSV/FK, day boundaries, no radio without candidates, radio="+radioCount+"/1000, floor/union/dedup/reset/neutral/overflow";
'@ | unity-cli exec
if ($LASTEXITCODE -ne 0 -or "$result" -notmatch 'PRICE_EVENTS_PASS:') { throw "Price event check failed: $result" }
$result
