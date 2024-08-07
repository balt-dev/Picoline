return {
    name = "PicoTrigger",
    placements = {
        {
            name = "pico8TriggerOn",
            data = {kind = "on"}
        },
        {
            name = "pico8TriggerOff",
            data = {kind = "off"}
        },
        {
            name = "pico8TriggerSwap",
            data = {kind = "swap"}
        }
    },
    fieldInformation = {
        kind = {
            fieldType = "string",
            default = "swap",
            options  = {swap = "swap", on = "on", off = "off"}
        }
    },
    triggerText = function(room, trigger)
        return "PICO-8 Trigger\n(" .. trigger.kind:sub(1, 1):upper() .. trigger.kind:sub(2) .. ")"
    end
}