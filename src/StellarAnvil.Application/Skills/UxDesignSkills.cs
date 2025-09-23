using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace StellarAnvil.Application.Skills;

/// <summary>
/// UX Design skills for creating HTML interfaces with React
/// </summary>
public class UxDesignSkills
{
    [KernelFunction, Description("Request HTML folder location for UI designs")]
    public Task<string> RequestHtmlFolderAsync(
        [Description("Description of the UI component to be created")] string componentDescription)
    {
        // Return OpenAI-compatible tool call for HTML folder location
        var toolCall = JsonSerializer.Serialize(new
        {
            type = "function",
            function = new
            {
                name = "provide_html_folder",
                arguments = JsonSerializer.Serialize(new
                {
                    message = "I need a location to save HTML designs. Please provide the HTML folder path where I should save the UI components.",
                    component_description = componentDescription,
                    required_structure = new
                    {
                        html_folder = "Path to HTML folder",
                        use_react = true,
                        include_css = true
                    }
                })
            }
        });

        return Task.FromResult(toolCall);
    }

    [KernelFunction, Description("Create HTML component with React")]
    public async Task<string> CreateHtmlComponentAsync(
        [Description("Component name")] string componentName,
        [Description("Component description and requirements")] string description,
        [Description("HTML folder path")] string htmlFolderPath,
        [Description("Include CSS styling")] bool includeCss = true)
    {
        try
        {
            if (string.IsNullOrEmpty(htmlFolderPath))
            {
                return await RequestHtmlFolderAsync($"HTML component: {componentName}");
            }

            // Create HTML structure
            var htmlContent = GenerateReactComponent(componentName, description, includeCss);

            // Create file path
            var componentFileName = $"{componentName.Replace(" ", "")}.html";
            var fullPath = Path.Combine(htmlFolderPath, componentFileName);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write HTML file
            await File.WriteAllTextAsync(fullPath, htmlContent);

            // Create CSS file if requested
            if (includeCss)
            {
                var cssContent = GenerateComponentCss(componentName);
                var cssPath = Path.Combine(htmlFolderPath, $"{componentName.Replace(" ", "")}.css");
                await File.WriteAllTextAsync(cssPath, cssContent);
            }

            return $"✅ Successfully created HTML component '{componentName}' at {fullPath}" +
                   (includeCss ? $"\n✅ CSS file created at {Path.Combine(htmlFolderPath, $"{componentName.Replace(" ", "")}.css")}" : "");
        }
        catch (Exception ex)
        {
            return $"❌ Error creating HTML component: {ex.Message}";
        }
    }

    [KernelFunction, Description("Generate React component template")]
    public Task<string> GenerateReactTemplateAsync(
        [Description("Component name")] string componentName,
        [Description("Component props and functionality")] string componentSpecs,
        [Description("UI framework (bootstrap, tailwind, material-ui)")] string uiFramework = "bootstrap")
    {
        var reactTemplate = GenerateReactComponentTemplate(componentName, componentSpecs, uiFramework);
        return Task.FromResult(reactTemplate);
    }

    [KernelFunction, Description("Create wireframe description")]
    public Task<string> CreateWireframeAsync(
        [Description("Page or component name")] string pageName,
        [Description("Layout requirements and elements")] string layoutRequirements)
    {
        var wireframe = $@"# Wireframe for {pageName}

## Layout Requirements:
{layoutRequirements}

## Suggested Structure:
1. Header Section
   - Navigation menu
   - Logo/branding
   - User actions (login/profile)

2. Main Content Area
   - Primary content based on requirements
   - Secondary content/sidebar if needed
   - Interactive elements

3. Footer Section
   - Links and information
   - Contact details
   - Copyright/legal

## React Component Architecture:
- {pageName}Component (main container)
- HeaderComponent
- ContentComponent
- FooterComponent

## Recommended UI Patterns:
- Responsive grid system
- Consistent spacing and typography
- Accessible form elements
- Loading states and error handling
- Mobile-first design approach

Would you like me to create the HTML implementation for this wireframe?";

        return Task.FromResult(wireframe);
    }

    private static string GenerateReactComponent(string componentName, string description, bool includeCss)
    {
        var cssLink = includeCss ? $"<link rel=\"stylesheet\" href=\"{componentName.Replace(" ", "")}.css\">" : "";
        var safeComponentName = componentName.Replace(" ", "");

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{componentName}</title>
    <script src=""https://unpkg.com/react@18/umd/react.development.js""></script>
    <script src=""https://unpkg.com/react-dom@18/umd/react-dom.development.js""></script>
    <script src=""https://unpkg.com/@babel/standalone/babel.min.js""></script>
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css"" rel=""stylesheet"">
    {cssLink}
</head>
<body>
    <div id=""root""></div>

    <script type=""text/babel"">
        const {{ useState }} = React;

        function {safeComponentName}() {{
            const [isLoading, setIsLoading] = useState(false);
            const [data, setData] = useState(null);

            const handleAction = () => {{
                setIsLoading(true);
                // TODO: Implement action based on: {description}
                setTimeout(() => {{
                    setIsLoading(false);
                    setData('Action completed!');
                }}, 1000);
            }};

            return (
                <div className=""container mt-4"">
                    <div className=""row"">
                        <div className=""col-12"">
                            <h1 className=""mb-4"">{componentName}</h1>
                            <div className=""card"">
                                <div className=""card-body"">
                                    <p className=""card-text"">{description}</p>
                                    {{data && <div className=""alert alert-success"" role=""alert"">{{data}}</div>}}
                                    <button
                                        className=""btn btn-primary""
                                        onClick={{handleAction}}
                                        disabled={{isLoading}}
                                    >
                                        {{isLoading ? 'Loading...' : 'Take Action'}}
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            );
        }}

        ReactDOM.render(<{safeComponentName} />, document.getElementById('root'));
    </script>
</body>
</html>";
    }

    private static string GenerateComponentCss(string componentName)
    {
        var safeComponentName = componentName.Replace(" ", "").ToLower();

        return $@"/* {componentName} Component Styles */

.{safeComponentName}-container {{
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    line-height: 1.6;
    color: #333;
}}

.{safeComponentName}-header {{
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    color: white;
    padding: 2rem 0;
    margin-bottom: 2rem;
    border-radius: 8px;
}}

.{safeComponentName}-card {{
    border: none;
    box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
    border-radius: 12px;
    transition: all 0.3s ease;
}}

.{safeComponentName}-card:hover {{
    transform: translateY(-2px);
    box-shadow: 0 8px 25px rgba(0, 0, 0, 0.15);
}}

.{safeComponentName}-button {{
    background: linear-gradient(45deg, #667eea, #764ba2);
    border: none;
    border-radius: 25px;
    padding: 12px 30px;
    font-weight: 600;
    transition: all 0.3s ease;
}}

.{safeComponentName}-button:hover {{
    transform: translateY(-1px);
    box-shadow: 0 4px 15px rgba(102, 126, 234, 0.4);
}}

.{safeComponentName}-button:disabled {{
    opacity: 0.6;
    transform: none;
    cursor: not-allowed;
}}

@media (max-width: 768px) {{
    .{safeComponentName}-container {{
        padding: 1rem;
    }}

    .{safeComponentName}-header {{
        padding: 1rem 0;
    }}
}}";
    }

    private static string GenerateReactComponentTemplate(string componentName, string componentSpecs, string uiFramework)
    {
        var safeComponentName = componentName.Replace(" ", "");
        var frameworkImports = uiFramework.ToLower() switch
        {
            "tailwind" => "// Tailwind CSS classes",
            "material-ui" => "// Material-UI components",
            _ => "// Bootstrap classes"
        };

        return $@"import React, {{ useState, useEffect }} from 'react';
{frameworkImports}

interface {safeComponentName}Props {{
    // Define props based on: {componentSpecs}
    title?: string;
    onAction?: () => void;
    className?: string;
}}

const {safeComponentName}: React.FC<{safeComponentName}Props> = ({{
    title = '{componentName}',
    onAction,
    className = ''
}}) => {{
    const [isLoading, setIsLoading] = useState<boolean>(false);
    const [data, setData] = useState<any>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {{
        // Component initialization logic
        // TODO: Implement based on: {componentSpecs}
    }}, []);

    const handleAction = async () => {{
        if (!onAction) return;

        try {{
            setIsLoading(true);
            setError(null);
            await onAction();
            // Handle success
        }} catch (err) {{
            setError(err instanceof Error ? err.message : 'An error occurred');
        }} finally {{
            setIsLoading(false);
        }}
    }};

    return (
        <div className={{`{safeComponentName.ToLower()}-component ${{className}}`}}>
            <h2>{{title}}</h2>
            {{error && (
                <div className=""alert alert-danger"" role=""alert"">
                    {{error}}
                </div>
            )}}
            <div className=""component-content"">
                {{/* TODO: Implement UI based on: {componentSpecs} */}}
                <p>Component specifications: {componentSpecs}</p>
                <button
                    onClick={{handleAction}}
                    disabled={{isLoading}}
                    className=""btn btn-primary""
                >
                    {{isLoading ? 'Loading...' : 'Action'}}
                </button>
            </div>
        </div>
    );
}};

export default {safeComponentName};";
    }
}