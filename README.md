---
page_type: sample
description: This sample demonstrates how to use the Microsoft Graph connector API to create a custom connector that indexes issues and repositories from GitHub.
products:
- ms-graph
- microsoft-graph-connectors-api
- github
languages:
- csharp
---

# Microsoft Graph GitHub connector sample

[![dotnet build](https://github.com/microsoftgraph/msgraph-sample-github-connector-dotnet/actions/workflows/dotnet.yml/badge.svg)](https://github.com/microsoftgraph/msgraph-sample-github-connector-dotnet/actions/workflows/dotnet.yml) ![License.](https://img.shields.io/badge/license-MIT-green.svg)

Microsoft Graph connectors let you add your own data to the semantic search index and have it power various Microsoft 365 experiences. This .NET application shows you how to use the [Microsoft Graph connector](https://learn.microsoft.com/graph/connecting-external-content-connectors-overview) API to create a custom connector that indexes issues and repositories from GitHub. This connector sample powers experiences such as Microsoft Search, Copilot in Teams, the Microsoft 365 App, and more.

## Experiences

The Microsoft Graph connector experiences that will be enabled in the sample include:

- [Microsoft Search](https://learn.microsoft.com/graph/connecting-external-content-experiences#microsoft-search)
- [Context IQ in Outlook on the web](https://learn.microsoft.com/graph/connecting-external-content-experiences#context-iq-in-outlook-on-the-web-preview)
- [Microsoft 365 Copilot](https://learn.microsoft.com/graph/connecting-external-content-experiences#microsoft-365-copilot-limited-preview)
- [Microsoft 365 app: Quick Access & My Content](https://learn.microsoft.com/graph/connecting-external-content-experiences#microsoft-365-app)
- Type Down Suggestions (Query formulation)
- Simplified admin experience in the Teams admin center (Microsoft 356 App)

## Prerequisites

- [.NET 7 SDK](https://dotnet.microsoft.com/download)
- A Microsoft work or school account with the Global administrator role. If you don't have a Microsoft account, you can [sign up for the Microsoft 365 Developer Program](https://developer.microsoft.com/microsoft-365/dev-program) to get a free Microsoft 365 subscription.
- A [GitHub account](https://github.com)

### Microsoft 365 app requirements

If you want to enable the [simplified admin experience in the Teams admin center](https://learn.microsoft.com/graph/connecting-external-content-deploy-teams), you will also need the following.

- The [devtunnel CLI](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started) or [ngrok](https://ngrok.com/).
- [Custom Teams app uploading](https://learn.microsoft.com/microsoftteams/platform/concepts/build-and-test/prepare-your-o365-tenant#enable-custom-teams-apps-and-turn-on-custom-app-uploading) must be enabled in your Microsoft 365 tenant.

## Register an app in Azure portal

1. Go to the [Azure Active Directory admin center](https://aad.portal.azure.com/) and sign in with an administrator account.
1. In the left pane, select **Azure Active Directory**, and under **Manage**, select **App registrations**.
1. Select **New registration**.
1. Complete the Register an application form with the following values, and then select **Register**.
    - **Name**: `GitHub Connector`
    - **Supported account types**: **Accounts in this organizational directory only**
    - **Redirect URI**: Leave blank

1. On the GitHub Connector **overview page**, copy the values of **Application (client) ID** and **Directory (tenant) ID**. You will need both in the following section.
1. Select **API Permissions** under Manage.
1. Remove the default **User.Read** permission under **Configured permissions** by selecting the ellipses (**...**) in its row and selecting **Remove permission**.
1. Select **Add a permission**, and then select **Microsoft Graph**.
1. Select **Application permissions**, and add the following permissions:
    - ExternalConnection.ReadWrite.OwnedBy
    - ExternalItem.ReadWrite.OwnedBy

1. Select **Grant admin consent for...**, and then select **Yes** when prompted.
1. Select **Certificates** & secrets under **Manage**, and then select **New client secret**.
1. Enter a description and choose an expiration time for the secret, and then select **Add**.
1. Copy and save the new secret. You will need it in the following section.

  > [!IMPORTANT]
  > This client secret is never shown again, so make sure you copy it now.

## Generate a GitHub personal access token

1. Login to your GitHub account and access your [profile page](https://github.com/settings/profile).
1. Select **Developer settings**.
1. Select **Personal access tokens**, choose **Fine-grained tokens**, then select **Generate new token**.
1. Complete the **New fine-grained personal access token** form with the following values, then select **Generate Token**
    - **Name**: Graph Connector
    - **Expiration**: 60 days
    - **Repository Access**: All repositories
    - **Repository Permissions**:  Set **Issues**, **Contents** and **Metadata** to **Read-only**.

1. Copy and save the newly generated token. You will need it in the following section.

## Configure the connector sample

1. Open [appsettings.json](./src/appsettings.json) and update the following values. Alternatively, make a copy of **appsettings.json** named **appsettings.Development.json** and change the values there.

    | Setting | Value |
    |---------|-------|
    | `clientId` | The **Application (client) ID** of your app registration in the Azure portal. |
    | `tenantId` | The **Directory (tenant) ID** of your app registration in the Azure portal. |
    | `gitHubRepoOwner` | The GitHub user or organization to read data from. |
    | `gitHubRepo` | The GitHub repository to ingest issues from. Must be owned by the user or organization set in `gitHubRepoOwner`. |
    | `portNumber` | The port number to listen on when using the simplified admin experience in the Teams admin center |
    | `placeholderUserId` | A user ID in your Microsoft 365 tenant. You can get the user ID of a user in the Azure portal. Select an Azure Active Directory user and copy the value of their **Object ID**. |

1. Open your command line interface (CLI) in the directory where **GitHubConnector.csproj** is located.

1. Run the following commands to store the client secret and GitHub token in the user secret store.

    ```bash
    dotnet user-secrets set settings:clientSecret "YOUR_CLIENT_SECRET_FROM_APP_REGISTRATION"
    dotnet user-secrets set settings:githubToken "YOUR_GITHUB_PERSONAL_ACCESS_TOKEN"
    ```

## Run the application to create a connection

This sample offers two ways of creating a connection. You can create one interactively, selecting steps from the sample's command line menu, or you can create a connector using the [simplified admin experience in the Teams admin center](https://learn.microsoft.com/graph/connecting-external-content-deploy-teams).

> [!NOTE]
> You do not have to do use both of these methods - once a connection is created, you can continue to the [Ingest items](#ingest-items) section.

### Create a connection in interactive mode

In this step, you will build and run the sample as an interactive console app. This code sample will create a new connection, register the schema, and then push GitHub repo or issues into that connection.

1. Open your command-line interface (CLI) in the directory where **GitHubConnector.csproj** is located.
1. Use the command `dotnet build` to build the sample.
1. Use the command `dotnet run` to run the sample.
1. Select **1. Create a connection**.
    - Enter a unique identifier (alphanumeric characters only), name, and description for that connection.
    - Select which GitHub content data will be ingested into the connection (repositories or issues).
1. Select **4. Register schema for current connection** option, and then wait for the operation to complete.
    - Select which schema to use (repositories or issues).

The connection is now ready to [ingest items](#ingest-items).

### Create a connection in simplified admin mode

There are additional configuration steps to run the sample in simplified admin mode.

### Create a dev tunnel

The simplified admin experience in the Teams admin center communicates with the sample connector by sending an HTTP POST request. In this section you will create a dev tunnel to allow the Teams admin center to send the POST to the sample running on your local development machine.

1. If you do not have the devtunnel CLI installed, follow [these instructions](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows#install) to install.
1. Run the following command to login to the dev tunnel service. You can login with either a Microsoft Azure Active Directory account, a Microsoft account, or a GitHub account.

    ```bash
    devtunnel user login
    ```

1. Run the following commands to create a tunnel. Copy the **Tunnel ID** from the output.

    ```bash
    devtunnel create --allow-anonymous
    ```

1. Run the following command to assign a port to the tunnel. Replace `tunnel-id` with the **Tunnel ID** copied in the previous step, and `port-number` with the HTTP port set in your **appsettings.json**.

    ```bash
    devtunnel port create tunnel-id -p port-number
    ```

1. Run the following command to host the tunnel. Replace `tunnel-id` with the **Tunnel ID** copied in the previous step.

    ```bash
    devtunnel host tunnel-id
    ```

1. Copy the URL labeled **Connect via browser**.

    > [!NOTE]
    > The output shows two URLs for **Connect via browser**. Be sure to copy only one of them.

### Create a Teams app package

1. Make a copy of the [sample-manifest.json](./simplified-admin/sample-manifest.json) named **manifest.json**.
1. In **manifest.json**, replace `YOUR_CLIENT_ID_HERE` with the **Application (client) ID** of your app registration in the Azure portal.
1. In **manifest.json**, replace `YOUR_DEV_TUNNEL_URL_HERE` with your dev tunnel URL.
1. Create a ZIP file containing **manifest.json**, **color.png**, and **outline.png**.

### Upload the app package

1. Open the [Microsoft Teams admin center](https://admin.teams.microsoft.com) in your browser.
1. Select **Teams apps**, then **Manage apps**.
1. Select **Upload new app**.
1. Select **Upload** in the pop-up, then browse to the ZIP file you created in the previous step.
1. Follow the prompts to confirm and upload the ZIP file.

### Run the application

1. Open your command-line interface (CLI) in the directory where **GitHubConnector.csproj** is located.
1. Use the command `dotnet build` to build the sample.
1. Use the command `dotnet run -- --use-simplified-admin` to run the sample.

### Enable the connector in Teams admin center

1. Open the [Microsoft Teams admin center](https://admin.teams.microsoft.com) in your browser.
1. Select **Teams apps**, then **Manage apps**.
1. Search for "GitHub Connector", then select **GitHub Connector Admin-dev**.
1. Select **Graph Connector**.
1. Toggle on the **Connection status**.

The connection is now ready to [ingest items](#ingest-items).

## Ingest items

1. Use the command `dotnet run` to run the sample in interactive mode.
1. Select **2. Select existing connection**. Select the connection you created before.
1. Select **5. Push items to current connection**.
    - Select which items to push to the current connection (repositories or issues).

## Surface the data in Search

In this step, you will create search verticals and result types to customize the search results in Microsoft SharePoint, Microsoft Office, and Microsoft Search in Bing.

### Create a vertical

1. Sign into the [Microsoft 365 admin center](https://admin.microsoft.com/) by using the global administrator role.
1. Select **Settings** > **Search & intelligence** > **Customizations**.
1. Select **Verticals**, then select **Add**.
1. Enter a name in the **Name** field and select **Next**.
1. Select **Connectors**, then select the connection you created previously. Select **Next**.
1. On the **Add a query page**, leave the query blank. Select **Next**.
1. On the **Filters** page, select **Next**.
1. Select **Add Vertical**
1. Select **Enable vertical**, then select **Done**.

> [!NOTE]
> It may take a few hours before your new vertical shows up in Microsoft Search.

## Code of conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
