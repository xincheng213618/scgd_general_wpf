using System;

namespace ColorVision.Copilot
{
    internal static class CopilotSettingsCommand
    {
        internal const string Usage =
            "用法：/settings [models|agent|web|mcp|sync]。省略参数时打开模型设置。";

        internal static bool TryResolvePage(
            string? arguments,
            out CopilotSettingsPage page)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0
                || string.Equals(normalized, "models", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "model", StringComparison.OrdinalIgnoreCase))
            {
                page = CopilotSettingsPage.Models;
                return true;
            }

            if (string.Equals(normalized, "agent", StringComparison.OrdinalIgnoreCase))
            {
                page = CopilotSettingsPage.Agent;
                return true;
            }

            if (string.Equals(normalized, "web", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "nat64", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "pref64", StringComparison.OrdinalIgnoreCase))
            {
                page = CopilotSettingsPage.Web;
                return true;
            }

            if (string.Equals(normalized, "mcp", StringComparison.OrdinalIgnoreCase))
            {
                page = CopilotSettingsPage.Mcp;
                return true;
            }

            if (string.Equals(normalized, "sync", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "backend", StringComparison.OrdinalIgnoreCase))
            {
                page = CopilotSettingsPage.BackendSync;
                return true;
            }

            page = CopilotSettingsPage.Models;
            return false;
        }
    }
}
