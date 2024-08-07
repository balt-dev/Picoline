return {
    name = "PicoRefill",
    depth = -100,
    placements = {
        {
            name = "pico8RefillOn",
            data = {kind = "on"}
        },
        {
            name = "pico8RefillOff",
            data = {kind = "off"}
        },
        {
            name = "pico8RefillSwap",
            data = {kind = "swap"}
        }
    },
    texture = function(room, entity)
        return "objects/picoRefill/" .. entity.kind .. "_idle00"
    end,
    fieldInformation = {
        kind = {
            fieldType = "string",
            default = "swap",
            options  = {swap = "swap", on = "on", off = "off"}
        }
    }
}
