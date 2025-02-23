class_name HttpRequestPool
extends Node

#signal request_sent(path: String, info: Variant)
signal request_completed(result: int, response_code: int, headers: PackedStringArray, body: PackedByteArray)

@export
var pool_size: int = 2:
    set (value):
        pool_size = value
    get:
        return pool_size

var _queue: Array[Variant] = []
var _free_requests: Array[HTTPRequest] = []

func _build_pool():
    for i in range(pool_size):
        var request = HTTPRequest.new()
        request.download_chunk_size = 6291456
        request.use_threads = true
        var callback = func(result: int, response_code: int, headers: PackedStringArray, body: PackedByteArray):
            _free_requests.push_back(request)
            _on_http_request_request_completed(result, response_code, headers, body)
        #request.request_completed.connect(_on_http_request_request_completed)
        request.request_completed.connect(callback)
        _free_requests.push_back(request)
        add_child(request)

func _ready() -> void:
    _build_pool()

func _process(delta: float) -> void:
    while _queue.size() > 0 && _free_requests.size() > 0:
        var queued = _queue.pop_front()
        var do_request = true
        if queued.callback != null:
            pass
            #do_request = queued.callback.call() == true
        if do_request:
            var request = _free_requests.pop_front()
            request.request(queued.path)
            #rint("requsting %s..." % queued.path)

func is_ready():
    return _free_requests.size() > 0

func request_now(path: String):
    var request = _free_requests.pop_front()
    if request == null:
        return false
    request.request(path)
    return true

func submit(path: String, callback: Callable):
    if callback == null:
        pass
    _queue.append({"path": path, "callback": callback})

func submit_front(path: String, callback: Callable):
    if callback == null:
        pass
    _queue.push_front({"path": path, "callback": callback})

func clear_queue():
    _queue.clear()

func _on_http_request_request_completed(result: int, response_code: int, headers: PackedStringArray, body: PackedByteArray):
    #print("received")
    request_completed.emit(result, response_code, headers, body)
