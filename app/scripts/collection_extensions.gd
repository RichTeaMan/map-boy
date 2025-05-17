func average_int(array: Array[int]) -> int:
    var cumm := 0
    for e in array:
        cumm += e
    @warning_ignore("integer_division")
    var result: int = cumm / array.size()
    return result

func average_float(array: Array[float]) -> float:
    var cumm := 0.0
    for e in array:
        cumm += e
    var result: float = cumm / array.size()
    return result

func select_many(array: Array) -> Array:
    var seq = []
    for e1 in array:
        for e2 in e1:
            seq.append(e2)
    return seq
