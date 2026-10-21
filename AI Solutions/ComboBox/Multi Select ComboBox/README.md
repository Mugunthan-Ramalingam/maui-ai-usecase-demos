# AI-Powered Multi-Select ComboBox in .NET MAUI

This sample demonstrates AI-driven selection with the Syncfusion .NET MAUI ComboBox. The application integrates Azure OpenAI to understand a natural-language request and automatically select the most relevant fruits in a multi-select ComboBox.

## Overview

Multi-select experiences often require users to identify multiple items from a long list. This sample makes that process smarter by allowing the user to describe their intent in plain language, while the AI maps the request to the available fruit list and updates the ComboBox selection automatically.

The solution is useful for scenarios such as guided filtering, product selection, preference matching, and intelligent item pickers inside MAUI applications.

## Key Features

- Syncfusion .NET MAUI ComboBox integration
- Azure OpenAI-powered intent-based selection
- AI matching against a fixed fruit menu
- Up to 10 matching fruit selections per response
- Natural-language request understanding
- Loading, error, clear, and selection states in the UI
- Checkbox-based dropdown rows with token-style selected values
- Cross-platform .NET MAUI application

## Sample

```xaml
<editors:SfComboBox x:Name="comboBox"
                    WidthRequest="500"
                    SelectionMode="Multiple"
                    IsEditable="False"
                    IsFilteringEnabled="False"
                    DisplayMemberPath="Name"
                    TextMemberPath="Name"
                    Placeholder="{Binding ComboHint}"
                    MultiSelectionDisplayMode="Token"
                    TokensWrapMode="Wrap"
                    ItemsSource="{Binding Fruits}"
                    SelectedItems="{Binding SelectedItems, Mode=TwoWay}">
    <editors:SfComboBox.ItemTemplate>
        <DataTemplate x:DataType="local:FruitModel">
            <Grid ColumnDefinitions="0.9*,0.1*">
                <Label Text="{Binding Name}" />
                <CheckBox IsChecked="{Binding IsSelected, Mode=TwoWay}" />
            </Grid>
        </DataTemplate>
    </editors:SfComboBox.ItemTemplate>
</editors:SfComboBox>
```

## AI integration

`FruitSelectionAzureAIService` uses Azure.AI.OpenAI 2.1.0 and the same typed SDK flow as the SmartTags sample: `AzureOpenAIClient`, `GetChatClient`, and `CompleteChatAsync`. The view model sends the fruit names and their descriptive tags with the user's request. It parses the response, ignores non-matches, removes duplicates, limits results to 10, and applies the matching names to the existing fruit models.

## Azure OpenAI setup

Configure the endpoint and deployment in `SmartAIComboBox/SmartAIComboBox/AIService/FruitSelectionAzureAIService.cs`. Replace the `ApiKey` placeholder there with a newly rotated key before running; the service reports an actionable error while the placeholder remains. Do not commit credentials. This sample calls Azure OpenAI directly from the MAUI client, so a packaged app can expose a bundled key. Use a secured backend service to protect credentials in production.

The project targets .NET 10 and uses the Azure.AI.OpenAI 2.1.0 NuGet package.

## Output

![AI MultiSelect ComboBox demo](smartcombobox_fruits.gif)

## Requirements to run the demo

To run the demo, refer to https://help.syncfusion.com/maui/system-requirements

## Prerequisites

Before running the sample, ensure that you have:

- Visual Studio with the .NET 10 SDK and .NET MAUI workload installed
- .NET 10 SDK with the .NET MAUI workload
- Azure OpenAI resource and deployment
- Syncfusion .NET MAUI Core and Inputs packages
- Valid Syncfusion license key

## Troubleshooting

### Path too long exception

If you encounter a "Path Too Long" exception while building the project, close Visual Studio, rename the repository folder to a shorter name, and rebuild the project.

## License

Syncfusion has no liability for any damage or consequence that may arise from using or viewing the samples. The samples are for demonstrative purposes. If you choose to use or access the samples, you agree to not hold Syncfusion liable, in any form, for any damage related to use, for accessing, or viewing the samples. By accessing, viewing, or seeing the samples, you acknowledge and agree Syncfusion's samples will not allow you seek injunctive relief in any form for any claim related to the sample. If you do not agree to this, do not view, access, utilize, or otherwise do anything with Syncfusion's samples.
