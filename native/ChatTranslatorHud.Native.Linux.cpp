#include <algorithm>
#include <atomic>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <queue>
#include <string>
#include <sys/mman.h>
#include <unistd.h>

#include <google/protobuf/descriptor.h>
#include <google/protobuf/message.h>
#include <google/protobuf/reflection.h>

#define NATIVE_EXPORT extern "C" __attribute__((visibility("default")))

class CNetMessage
{
public:
    virtual ~CNetMessage() = default;
    virtual void* AsProto() const = 0;
    virtual void* AsProto2() const = 0;
};

struct ChatTranslatorHudConVarResponse
{
    int32_t cookie;
    int32_t status_code;
    int32_t name_length;
    int32_t value_length;
    char name[512];
    char value[512];
};

static std::mutex g_ResponseMutex;
static std::queue<ChatTranslatorHudConVarResponse> g_ResponseQueue;

using ProcessRespondCvarValue_t = bool (*)(void* pClient, const void* pData);

static std::mutex g_HookMutex;
static void** g_VtableEntry = nullptr;
static void* g_OriginalEntry = nullptr;
static int g_OriginalProtection = PROT_READ;
static ProcessRespondCvarValue_t g_OriginalProcessRespondCvarValue = nullptr;

static std::atomic<int32_t> g_HookCalls{0};
static std::atomic<int32_t> g_HookHits{0};
static std::atomic<int32_t> g_ParseFailures{0};

static int ProtectionFromPermissions(const char* permissions)
{
    int protection = 0;
    if (permissions[0] == 'r') protection |= PROT_READ;
    if (permissions[1] == 'w') protection |= PROT_WRITE;
    if (permissions[2] == 'x') protection |= PROT_EXEC;
    return protection == 0 ? PROT_READ : protection;
}

static bool TryGetMemoryProtection(void* address, int* protection)
{
    FILE* maps = std::fopen("/proc/self/maps", "r");
    if (maps == nullptr)
        return false;

    const auto target = reinterpret_cast<uintptr_t>(address);
    char line[512];
    while (std::fgets(line, sizeof(line), maps) != nullptr)
    {
        uintptr_t start = 0;
        uintptr_t end = 0;
        char permissions[5] = {};
        if (std::sscanf(line, "%lx-%lx %4s", &start, &end, permissions) == 3 &&
            target >= start && target < end)
        {
            *protection = ProtectionFromPermissions(permissions);
            std::fclose(maps);
            return true;
        }
    }

    std::fclose(maps);
    return false;
}

static bool WriteVtableEntry(void** entry, void* value, int originalProtection)
{
    if (entry == nullptr)
        return false;

    const long pageSize = sysconf(_SC_PAGESIZE);
    if (pageSize <= 0)
        return false;

    const auto pageStart = reinterpret_cast<uintptr_t>(entry) & ~(static_cast<uintptr_t>(pageSize) - 1);
    const auto* page = reinterpret_cast<void*>(pageStart);
    const int writableProtection = originalProtection | PROT_WRITE;

    if (mprotect(const_cast<void*>(page), static_cast<size_t>(pageSize), writableProtection) != 0)
        return false;

    *entry = value;

    if (mprotect(const_cast<void*>(page), static_cast<size_t>(pageSize), originalProtection) != 0)
        return false;

    return true;
}

static bool TryReadInt32(
    const google::protobuf::Message& message,
    const google::protobuf::Reflection& reflection,
    const char* fieldName,
    int32_t* value)
{
    const auto* field = message.GetDescriptor()->FindFieldByName(fieldName);
    if (field == nullptr || field->is_repeated() || !reflection.HasField(message, field))
        return false;

    switch (field->cpp_type())
    {
        case google::protobuf::FieldDescriptor::CPPTYPE_INT32:
            *value = reflection.GetInt32(message, field);
            return true;
        case google::protobuf::FieldDescriptor::CPPTYPE_UINT32:
            *value = static_cast<int32_t>(reflection.GetUInt32(message, field));
            return true;
        case google::protobuf::FieldDescriptor::CPPTYPE_ENUM:
            *value = reflection.GetEnumValue(message, field);
            return true;
        default:
            return false;
    }
}

static bool TryReadString(
    const google::protobuf::Message& message,
    const google::protobuf::Reflection& reflection,
    const char* fieldName,
    std::string* value)
{
    const auto* field = message.GetDescriptor()->FindFieldByName(fieldName);
    if (field == nullptr ||
        field->is_repeated() ||
        field->cpp_type() != google::protobuf::FieldDescriptor::CPPTYPE_STRING ||
        !reflection.HasField(message, field))
    {
        return false;
    }

    *value = reflection.GetString(message, field);
    return true;
}

static bool TryExtractResponse(const void* pData, ChatTranslatorHudConVarResponse* response)
{
    if (pData == nullptr || response == nullptr)
        return false;

    const auto* netMessage = reinterpret_cast<const CNetMessage*>(pData);
    const auto* proto = static_cast<const google::protobuf::Message*>(netMessage->AsProto());
    if (proto == nullptr)
        return false;

    const auto* reflection = proto->GetReflection();
    if (reflection == nullptr)
        return false;

    int32_t cookie = 0;
    int32_t statusCode = 0;
    std::string name;
    std::string value;

    if (!TryReadInt32(*proto, *reflection, "cookie", &cookie) ||
        !TryReadInt32(*proto, *reflection, "status_code", &statusCode) ||
        !TryReadString(*proto, *reflection, "name", &name))
    {
        return false;
    }

    TryReadString(*proto, *reflection, "value", &value);

    if (name != "cl_language" && name != "cl_country")
        return false;

    std::memset(response, 0, sizeof(*response));
    response->cookie = cookie;
    response->status_code = statusCode;

    const size_t nameLength = std::min(name.size(), sizeof(response->name) - 1);
    const size_t valueLength = std::min(value.size(), sizeof(response->value) - 1);
    std::memcpy(response->name, name.data(), nameLength);
    std::memcpy(response->value, value.data(), valueLength);
    response->name[nameLength] = '\0';
    response->value[valueLength] = '\0';
    response->name_length = static_cast<int32_t>(nameLength);
    response->value_length = static_cast<int32_t>(valueLength);
    return true;
}

static bool Detour_ProcessRespondCvarValue(void* pClient, const void* pData)
{
    g_HookCalls++;

    ChatTranslatorHudConVarResponse response;
    if (TryExtractResponse(pData, &response))
    {
        g_HookHits++;
        std::lock_guard<std::mutex> lock(g_ResponseMutex);
        g_ResponseQueue.push(response);
    }
    else
    {
        g_ParseFailures++;
    }

    if (g_OriginalProcessRespondCvarValue != nullptr)
        return g_OriginalProcessRespondCvarValue(pClient, pData);

    return true;
}

NATIVE_EXPORT int ChatTranslatorHud_NativeVersion()
{
    return 8;
}

NATIVE_EXPORT int ChatTranslatorHud_InitHook(void* targetFunction)
{
    (void)targetFunction;
    return -5;
}

NATIVE_EXPORT int ChatTranslatorHud_InitHookVTable(void* vtable, int32_t vtableIndex)
{
    if (vtable == nullptr || vtableIndex < 0)
        return -1;

    std::lock_guard<std::mutex> lock(g_HookMutex);
    if (g_VtableEntry != nullptr)
    {
        return g_VtableEntry == (reinterpret_cast<void**>(vtable) + vtableIndex) ? 0 : -2;
    }

    auto** entry = reinterpret_cast<void**>(vtable) + vtableIndex;
    void* original = *entry;
    if (original == nullptr)
        return -3;

    int originalProtection = PROT_READ;
    TryGetMemoryProtection(entry, &originalProtection);

    if (!WriteVtableEntry(entry, reinterpret_cast<void*>(&Detour_ProcessRespondCvarValue), originalProtection))
        return -4;

    g_VtableEntry = entry;
    g_OriginalEntry = original;
    g_OriginalProtection = originalProtection;
    g_OriginalProcessRespondCvarValue = reinterpret_cast<ProcessRespondCvarValue_t>(original);
    return 0;
}

NATIVE_EXPORT void ChatTranslatorHud_ShutdownHook()
{
    std::lock_guard<std::mutex> lock(g_HookMutex);
    if (g_VtableEntry != nullptr && g_OriginalEntry != nullptr)
    {
        WriteVtableEntry(g_VtableEntry, g_OriginalEntry, g_OriginalProtection);
    }

    g_VtableEntry = nullptr;
    g_OriginalEntry = nullptr;
    g_OriginalProtection = PROT_READ;
    g_OriginalProcessRespondCvarValue = nullptr;
}

NATIVE_EXPORT bool ChatTranslatorHud_HasResponse()
{
    std::lock_guard<std::mutex> lock(g_ResponseMutex);
    return !g_ResponseQueue.empty();
}

NATIVE_EXPORT bool ChatTranslatorHud_PopResponse(ChatTranslatorHudConVarResponse* response)
{
    if (response == nullptr)
        return false;

    std::lock_guard<std::mutex> lock(g_ResponseMutex);
    if (g_ResponseQueue.empty())
        return false;

    *response = g_ResponseQueue.front();
    g_ResponseQueue.pop();
    return true;
}

NATIVE_EXPORT int32_t ChatTranslatorHud_GetScanCalls()
{
    return g_HookCalls.load();
}

NATIVE_EXPORT int32_t ChatTranslatorHud_GetScanHits()
{
    return g_HookHits.load();
}

NATIVE_EXPORT int32_t ChatTranslatorHud_GetScanExceptions()
{
    return g_ParseFailures.load();
}
