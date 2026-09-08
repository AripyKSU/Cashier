$ErrorActionPreference = 'Stop'
$result = @'
var dispositions=Util.ParseFromCSV<CustomerDispositionData>(File.ReadAllText("Assets/Datas/Customer/CustomerDispositionData.csv")).ToDictionary(x=>x.Idx);
var products=Util.ParseFromCSV<ProductData>(File.ReadAllText("Assets/Datas/Customer/ProductData.csv")).ToDictionary(x=>x.Idx);
var config=dispositions[6002]; // 9초: 3초에 재촉
var generator=new CustomerGenerator(new System.Random(1));
uint price=100;int created=0;
var q=new CustomerQueue(()=>{created++;return generator.Generate(new uint[]{5001},new[]{config},products,0,()=>products.ToDictionary(x=>x.Key,x=>price));},dispositions);
q.Start();q.TryAdd();var first=q.Waiting[0];
q.Advance(200,true);if(q.Waiting.Count!=1 || first.Visit.State!=CustomerState.Queued)throw new Exception("Pause");
q.Advance(2,false);if(q.GetSpeech(first)!=0)throw new Exception("Early warning");
q.Advance(1,false);if(q.GetSpeech(first)!=config.QueueWarningTextIdx)throw new Exception("6-second warning");
q.Advance(3,false);if(q.GetSpeech(first)!=0)throw new Exception("Warning repeated");
while(q.TryAdd()) {} if(q.Waiting.Count!=10 || created!=10 || q.TryAdd())throw new Exception("Capacity/skip");
price=999;q.Advance(3,false);
if(first.Visit.State!=CustomerState.Abandoned || q.GetSpeech(first)!=config.QueueLeaveTextIdx)throw new Exception("Expiry/complaint");
var next=q.TakeNext();if(next.State!=CustomerState.Entering || next.Items.Any(x=>x.UnitPrice!=100))throw new Exception("FIFO/old prices");
next.BeginOffer();q.Advance(200,false);if(next.State!=CustomerState.AwaitingOffer)throw new Exception("Counter patience");
if(!q.Waiting.Any(x=>x.Visit.Items.All(y=>y.UnitPrice==999)))throw new Exception("New prices");
var remaining=q.Waiting.Select(x=>x.Visit).ToArray();q.Stop();if(q.Waiting.Count!=0 || q.Leaving.Count!=0 || remaining.Any(x=>x.State!=CustomerState.Departed))throw new Exception("Close cleanup");
q.Start();q.TryAdd();var hitch=q.Waiting[0];q.Advance(9,false);
if(hitch.Visit.State!=CustomerState.Abandoned || q.GetSpeech(hitch)!=config.QueueLeaveTextIdx)throw new Exception("Hitch expiry priority");
q.Advance(3,false);if(q.Leaving.Contains(hitch))throw new Exception("Complaint cleanup");
return "CUSTOMER_QUEUE_PASS: 5s arrivals, cap 10, pause, 6s warning once, expiry, FIFO, price snapshot, counter exclusion, close, hitch";
'@ | unity-cli exec
if ($LASTEXITCODE -ne 0 -or "$result" -notmatch 'CUSTOMER_QUEUE_PASS:') { throw "Queue check failed: $result" }
$result
