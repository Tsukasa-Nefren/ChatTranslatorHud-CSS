#include <algorithm>
#include <cstdint>
#include <cstring>
#include <functional>
#include <map>
#include <memory>
#include <string>
#include <vector>

#include "dynohook/platform.h"
#include "dynohook/core.h"
#include "dynohook/hook.h"
#include "netmessages.pb.h"

#if defined(_WIN32)
#define NATIVE_EXPORT extern "C" __declspec(dllexport)
#else
#define NATIVE_EXPORT extern "C" __attribute__((visibility("default")))
#endif

class INetworkMessageInternal;

template <typename PROTO_TYPE>
class CNetMessagePB;

// CAUTION: This fake layout must match the engine's CNetMessage class exactly.
// Fields (unknown1, margin, unknown2) are padding to reach the correct offset
// where CNetMessagePB<T> inherits PROTO_TYPE.
// Any engine update that changes CNetMessage layout will break this cast.
// This is standard CS2 modding practice; when the engine updates,
// Metamod/CSS/all plugins must be rebuilt to match.
class CNetMessage
{
public:
    CNetMessage() = delete;
    CNetMessage(const CNetMessage&) = delete;

    virtual ~CNetMessage() {}
    virtual void* AsProto() const = 0;
    virtual void* AsProto2() const = 0;
    virtual INetworkMessageInternal* GetNetMessage() const = 0;
    virtual CNetMessage* CopyConstruct(const CNetMessage* other) const = 0;

private:
    char unknown1[24];
    float margin;
    char unknown2[12];
};

// CAUTION: Direct static_cast from CNetMessage* to CNetMessagePB<CCLCMsg_RespondCvarValue>*
// assumes the caller has verified this is the correct message type.
// This is guaranteed by the vtable hook: the hook is installed on a specific
// CServerSideClient vtable slot (Windows: 38, Linux: 40) that only processes
// CCLCMsg_RespondCvarValue messages.
template <typename PROTO_TYPE>
class CNetMessagePB : public CNetMessage, public PROTO_TYPE
{
public:
    CNetMessagePB() = delete;
    CNetMessagePB(const CNetMessagePB&) = delete;
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

static int32_t CopyString(char* destination, size_t destinationSize, const std::string& source)
{
    if (destination == nullptr || destinationSize == 0)
        return 0;

    const auto length = std::min(source.size(), destinationSize - 1);
    std::memcpy(destination, source.data(), length);
    destination[length] = '\0';
    return static_cast<int32_t>(length);
}

NATIVE_EXPORT int ChatTranslatorHud_NativeVersion()
{
    return 4;
}

NATIVE_EXPORT void* ChatTranslatorHud_AsProto(const CNetMessage* message)
{
    return message == nullptr ? nullptr : message->AsProto();
}

NATIVE_EXPORT bool ChatTranslatorHud_ReadRespondCvarValue(
    const CNetMessage* message,
    const int32_t* expectedCookies,
    int32_t expectedCookieCount,
    ChatTranslatorHudConVarResponse* response)
{
    if (message == nullptr || expectedCookies == nullptr || expectedCookieCount <= 0 || response == nullptr)
        return false;

    std::memset(response, 0, sizeof(ChatTranslatorHudConVarResponse));

    const auto msg = static_cast<const CNetMessagePB<CCLCMsg_RespondCvarValue>*>(message);
    if (msg == nullptr || !msg->has_cookie())
        return false;

    const auto cookie = msg->cookie();
    auto cookieExpected = false;
    for (auto i = 0; i < expectedCookieCount; ++i)
    {
        if (expectedCookies[i] == cookie)
        {
            cookieExpected = true;
            break;
        }
    }

    if (!cookieExpected)
        return false;

    response->cookie = cookie;
    response->status_code = msg->has_status_code() ? msg->status_code() : 0;

    if (msg->has_name())
        response->name_length = CopyString(response->name, sizeof(response->name), msg->name());

    if (msg->has_value())
        response->value_length = CopyString(response->value, sizeof(response->value), msg->value());

    return response->cookie != 0 && response->name_length > 0;
}

NATIVE_EXPORT bool ChatTranslatorHud_ReadRespondCvarValueFromHook(
    const dyno::Hook* hook,
    const int32_t* expectedCookies,
    int32_t expectedCookieCount,
    ChatTranslatorHudConVarResponse* response)
{
    if (hook == nullptr)
        return false;

    // DynoHook convention: arg(0) = this pointer, arg(1) = first function parameter.
    // For CServerSideClient::ProcessRespondCvarValue(const CNetMessage*),
    // arg(1) is the CNetMessage pointer.
    const auto message = hook->getArgument<const CNetMessage*>(1);
    return ChatTranslatorHud_ReadRespondCvarValue(message, expectedCookies, expectedCookieCount, response);
}
