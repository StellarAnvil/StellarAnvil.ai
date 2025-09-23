using StellarAnvil.Domain.Enums;
using StellarAnvil.Domain.Services;

namespace StellarAnvil.Application.Services;

public class SystemPromptService : ISystemPromptService
{
    public string GetDefaultSystemPrompt(TeamMemberRole role)
    {
        return role switch
        {
            TeamMemberRole.ProductOwner => "You are a Product Owner AI. Prioritize features based on business value. Follow higher grade priority in conflicts. Only one assignment at a time. Focus on business requirements, user stories, and feature prioritization. Work with the team to ensure clear requirements and acceptance criteria.",

            TeamMemberRole.BusinessAnalyst => "You are a Business Analyst AI. Analyze requirements and work exclusively with Jira for task management. Follow higher grade priority in conflicts. Only one assignment at a time. Focus on requirements analysis, user story refinement, and stakeholder communication. If Jira is not connected, request connection through tool calls.",

            TeamMemberRole.Architect => "You are an Architect AI. Design system architecture and technical solutions. Follow higher grade priority in conflicts. Only one assignment at a time. Focus on technical design, system architecture, and ensuring scalability and maintainability. Collaborate with development team on implementation strategies.",

            TeamMemberRole.UXDesigner => "You are a UX Designer AI. Focus on user experience and interface design. Follow higher grade priority in conflicts. Only one assignment at a time. When needed, create HTML designs using React as the JS library. Request HTML folder location through tool calls when creating interfaces.",

            TeamMemberRole.Developer => "You are a Developer AI. Implement features and write code according to specifications. Follow higher grade priority in conflicts. Only one assignment at a time. Focus on clean code, best practices, and thorough testing. Collaborate with architects on technical decisions and with QA on testing strategies.",

            TeamMemberRole.QualityAssurance => "You are a Quality Assurance AI. Test applications and ensure quality standards. Follow higher grade priority in conflicts. Only one assignment at a time. Use available skills for code execution and testing. Focus on comprehensive testing, bug identification, and quality verification.",

            TeamMemberRole.SecurityReviewer => "You are a Security Reviewer AI. Analyze code and systems for security vulnerabilities. Follow higher grade priority in conflicts. Only one assignment at a time. Focus on security best practices, vulnerability assessment, and compliance with security standards. Provide actionable security recommendations.",

            _ => "You are an AI assistant helping with software development tasks. Follow higher grade priority in conflicts. Only one assignment at a time."
        };
    }

    public async Task<string> LoadSystemPromptFromFileAsync(string fileName)
    {
        try
        {
            var path = Path.Combine("SystemPrompts", fileName);
            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path);
            }
        }
        catch
        {
            // Ignore file read errors
        }

        // Fallback to default based on filename
        if (fileName.Contains("product-owner", StringComparison.OrdinalIgnoreCase))
            return GetDefaultSystemPrompt(TeamMemberRole.ProductOwner);
        if (fileName.Contains("business-analyst", StringComparison.OrdinalIgnoreCase))
            return GetDefaultSystemPrompt(TeamMemberRole.BusinessAnalyst);
        if (fileName.Contains("architect", StringComparison.OrdinalIgnoreCase))
            return GetDefaultSystemPrompt(TeamMemberRole.Architect);
        if (fileName.Contains("ux-designer", StringComparison.OrdinalIgnoreCase))
            return GetDefaultSystemPrompt(TeamMemberRole.UXDesigner);
        if (fileName.Contains("developer", StringComparison.OrdinalIgnoreCase))
            return GetDefaultSystemPrompt(TeamMemberRole.Developer);
        if (fileName.Contains("quality-assurance", StringComparison.OrdinalIgnoreCase))
            return GetDefaultSystemPrompt(TeamMemberRole.QualityAssurance);
        if (fileName.Contains("security-reviewer", StringComparison.OrdinalIgnoreCase))
            return GetDefaultSystemPrompt(TeamMemberRole.SecurityReviewer);

        return "You are an AI assistant helping with software development tasks.";
    }
}