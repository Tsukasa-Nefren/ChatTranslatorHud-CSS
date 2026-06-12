#define NOMINMAX
#include <algorithm>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <queue>
#include <string>
#include <Windows.h>

#include "MinHook.h"

#if defined(_WIN32)
#define NATIVE_EXPORT extern "C" __declspec(dllexport)
#else
#define NATIVE_EXPORT extern "C" __attribute__((visibility("default")))
#endif

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

typedef bool (*ProcessRespondCvarValue_t)(void* pClient, const void* pData);
ProcessRespondCvarValue_t g_pOriginal_ProcessRespondCvarValue = nullptr;

bool SafeReadString(void* pStrPtr, char* out_buf, size_t out_size) {
    if (!pStrPtr) return false;
    bool success = false;
#if defined(_WIN32)
    __try {
        uintptr_t str_obj = (*reinterpret_cast<uintptr_t*>(pStrPtr)) & ~3;
        if (str_obj != 0) {
            size_t length = *reinterpret_cast<size_t*>(str_obj + 16);
            size_t capacity = *reinterpret_cast<size_t*>(str_obj + 24);
            if (length < 1024 && capacity < 1024) {
                const char* cstr = nullptr;
                if (capacity < 16) {
                    cstr = reinterpret_cast<const char*>(str_obj);
                } else {
                    cstr = *reinterpret_cast<const char* const*>(str_obj);
                }
                
                if (cstr) {
                    size_t copy_len = std::min(length, out_size - 1);
                    std::memcpy(out_buf, cstr, copy_len);
                    out_buf[copy_len] = '\0';
                    success = true;
                }
            }
        }
    } __except (EXCEPTION_EXECUTE_HANDLER) {
        success = false;
    }
#else
    // GCC libstdc++ std::string ABI (Linux)
    // ptr, length, union { buf[16], capacity }
    try {
        uintptr_t str_obj = (*reinterpret_cast<uintptr_t*>(pStrPtr)) & ~3;
        if (str_obj != 0) {
            const char* cstr = *reinterpret_cast<const char* const*>(str_obj);
            size_t length = *reinterpret_cast<size_t*>(str_obj + 8);
            if (length < 1024 && cstr != nullptr) {
                size_t copy_len = std::min(length, out_size - 1);
                std::memcpy(out_buf, cstr, copy_len);
                out_buf[copy_len] = '\0';
                success = true;
            }
        }
    } catch (...) {
        success = false;
    }
#endif
    return success;
}

bool SafeReadInt32(const void* ptr, int32_t* out_val) {
    bool success = false;
#if defined(_WIN32)
    __try {
        *out_val = *reinterpret_cast<const int32_t*>(ptr);
        success = true;
    } __except(EXCEPTION_EXECUTE_HANDLER) {
        success = false;
    }
#else
    try {
        *out_val = *reinterpret_cast<const int32_t*>(ptr);
        success = true;
    } catch (...) {
        success = false;
    }
#endif
    return success;
}

bool Detour_ProcessRespondCvarValue(void* pClient, const void* pData)
{
    if (pData != nullptr)
    {
        const uintptr_t* scan_start = reinterpret_cast<const uintptr_t*>(pData);
        const int scan_count = 256 / sizeof(uintptr_t);
        
        for (int i = 0; i < scan_count - 2; ++i)
        {
            void* pPotentialNamePtr = (void*)(scan_start + i);
            char name_buf[64] = {0};
            
            if (SafeReadString(pPotentialNamePtr, name_buf, sizeof(name_buf)))
            {
                if (std::strcmp(name_buf, "cl_language") == 0 || std::strcmp(name_buf, "cl_country") == 0)
                {
                    void* pPotentialValuePtr = (void*)(scan_start + i + 1);
                    int32_t cookie = 0;
                    SafeReadInt32(scan_start + i + 2, &cookie);
                    
                    char value_buf[512] = {0};
                    SafeReadString(pPotentialValuePtr, value_buf, sizeof(value_buf));
                    
                    ChatTranslatorHudConVarResponse response;
                    std::memset(&response, 0, sizeof(response));
                    response.cookie = cookie;
                    response.status_code = 0;
                    std::strncpy(response.name, name_buf, sizeof(response.name) - 1);
                    std::strncpy(response.value, value_buf, sizeof(response.value) - 1);
                    response.name_length = std::strlen(response.name);
                    response.value_length = std::strlen(response.value);
                    
                    std::lock_guard<std::mutex> lock(g_ResponseMutex);
                    g_ResponseQueue.push(response);
                    break;
                }
            }
        }
    }

    if (g_pOriginal_ProcessRespondCvarValue)
        return g_pOriginal_ProcessRespondCvarValue(pClient, pData);
        
    return true;
}

NATIVE_EXPORT int ChatTranslatorHud_NativeVersion()
{
    return 7;
}

NATIVE_EXPORT int ChatTranslatorHud_InitHook(void* targetFunction)
{
    if (targetFunction == nullptr) return -1;

    if (MH_Initialize() != MH_OK && MH_Initialize() != MH_ERROR_ALREADY_INITIALIZED)
        return -2;

    if (MH_CreateHook(targetFunction, (LPVOID)&Detour_ProcessRespondCvarValue, reinterpret_cast<LPVOID*>(&g_pOriginal_ProcessRespondCvarValue)) != MH_OK)
        return -3;

    if (MH_EnableHook(targetFunction) != MH_OK)
        return -4;

    return 0;
}

NATIVE_EXPORT bool ChatTranslatorHud_PopResponse(ChatTranslatorHudConVarResponse* response)
{
    if (response == nullptr) return false;

    std::lock_guard<std::mutex> lock(g_ResponseMutex);
    if (g_ResponseQueue.empty()) return false;

    *response = g_ResponseQueue.front();
    g_ResponseQueue.pop();
    return true;
}

