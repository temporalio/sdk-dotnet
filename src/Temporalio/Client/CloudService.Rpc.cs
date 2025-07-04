// <auto-generated>
//     Generated. DO NOT EDIT!
// </auto-generated>
#pragma warning disable 8669
using System.Threading.Tasks;
using Temporalio.Api.Cloud.CloudService.V1;

namespace Temporalio.Client
{
    public abstract partial class CloudService
    {
        /// <summary>
        /// Invoke AddNamespaceRegion.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<AddNamespaceRegionResponse> AddNamespaceRegionAsync(AddNamespaceRegionRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("AddNamespaceRegion", req, AddNamespaceRegionResponse.Parser, options);
        }

        /// <summary>
        /// Invoke AddUserGroupMember.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<AddUserGroupMemberResponse> AddUserGroupMemberAsync(AddUserGroupMemberRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("AddUserGroupMember", req, AddUserGroupMemberResponse.Parser, options);
        }

        /// <summary>
        /// Invoke CreateApiKey.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<CreateApiKeyResponse> CreateApiKeyAsync(CreateApiKeyRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("CreateApiKey", req, CreateApiKeyResponse.Parser, options);
        }

        /// <summary>
        /// Invoke CreateNamespace.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<CreateNamespaceResponse> CreateNamespaceAsync(CreateNamespaceRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("CreateNamespace", req, CreateNamespaceResponse.Parser, options);
        }

        /// <summary>
        /// Invoke CreateNamespaceExportSink.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<CreateNamespaceExportSinkResponse> CreateNamespaceExportSinkAsync(CreateNamespaceExportSinkRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("CreateNamespaceExportSink", req, CreateNamespaceExportSinkResponse.Parser, options);
        }

        /// <summary>
        /// Invoke CreateNexusEndpoint.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<CreateNexusEndpointResponse> CreateNexusEndpointAsync(CreateNexusEndpointRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("CreateNexusEndpoint", req, CreateNexusEndpointResponse.Parser, options);
        }

        /// <summary>
        /// Invoke CreateServiceAccount.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<CreateServiceAccountResponse> CreateServiceAccountAsync(CreateServiceAccountRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("CreateServiceAccount", req, CreateServiceAccountResponse.Parser, options);
        }

        /// <summary>
        /// Invoke CreateUser.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<CreateUserResponse> CreateUserAsync(CreateUserRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("CreateUser", req, CreateUserResponse.Parser, options);
        }

        /// <summary>
        /// Invoke CreateUserGroup.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<CreateUserGroupResponse> CreateUserGroupAsync(CreateUserGroupRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("CreateUserGroup", req, CreateUserGroupResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DeleteApiKey.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DeleteApiKeyResponse> DeleteApiKeyAsync(DeleteApiKeyRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DeleteApiKey", req, DeleteApiKeyResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DeleteNamespace.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DeleteNamespaceResponse> DeleteNamespaceAsync(DeleteNamespaceRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DeleteNamespace", req, DeleteNamespaceResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DeleteNamespaceExportSink.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DeleteNamespaceExportSinkResponse> DeleteNamespaceExportSinkAsync(DeleteNamespaceExportSinkRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DeleteNamespaceExportSink", req, DeleteNamespaceExportSinkResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DeleteNamespaceRegion.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DeleteNamespaceRegionResponse> DeleteNamespaceRegionAsync(DeleteNamespaceRegionRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DeleteNamespaceRegion", req, DeleteNamespaceRegionResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DeleteNexusEndpoint.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DeleteNexusEndpointResponse> DeleteNexusEndpointAsync(DeleteNexusEndpointRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DeleteNexusEndpoint", req, DeleteNexusEndpointResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DeleteServiceAccount.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DeleteServiceAccountResponse> DeleteServiceAccountAsync(DeleteServiceAccountRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DeleteServiceAccount", req, DeleteServiceAccountResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DeleteUser.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DeleteUserResponse> DeleteUserAsync(DeleteUserRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DeleteUser", req, DeleteUserResponse.Parser, options);
        }

        /// <summary>
        /// Invoke DeleteUserGroup.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<DeleteUserGroupResponse> DeleteUserGroupAsync(DeleteUserGroupRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("DeleteUserGroup", req, DeleteUserGroupResponse.Parser, options);
        }

        /// <summary>
        /// Invoke FailoverNamespaceRegion.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<FailoverNamespaceRegionResponse> FailoverNamespaceRegionAsync(FailoverNamespaceRegionRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("FailoverNamespaceRegion", req, FailoverNamespaceRegionResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetAccount.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetAccountResponse> GetAccountAsync(GetAccountRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetAccount", req, GetAccountResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetApiKey.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetApiKeyResponse> GetApiKeyAsync(GetApiKeyRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetApiKey", req, GetApiKeyResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetApiKeys.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetApiKeysResponse> GetApiKeysAsync(GetApiKeysRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetApiKeys", req, GetApiKeysResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetAsyncOperation.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetAsyncOperationResponse> GetAsyncOperationAsync(GetAsyncOperationRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetAsyncOperation", req, GetAsyncOperationResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetNamespace.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetNamespaceResponse> GetNamespaceAsync(GetNamespaceRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetNamespace", req, GetNamespaceResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetNamespaceExportSink.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetNamespaceExportSinkResponse> GetNamespaceExportSinkAsync(GetNamespaceExportSinkRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetNamespaceExportSink", req, GetNamespaceExportSinkResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetNamespaceExportSinks.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetNamespaceExportSinksResponse> GetNamespaceExportSinksAsync(GetNamespaceExportSinksRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetNamespaceExportSinks", req, GetNamespaceExportSinksResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetNamespaces.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetNamespacesResponse> GetNamespacesAsync(GetNamespacesRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetNamespaces", req, GetNamespacesResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetNexusEndpoint.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetNexusEndpointResponse> GetNexusEndpointAsync(GetNexusEndpointRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetNexusEndpoint", req, GetNexusEndpointResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetNexusEndpoints.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetNexusEndpointsResponse> GetNexusEndpointsAsync(GetNexusEndpointsRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetNexusEndpoints", req, GetNexusEndpointsResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetRegion.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetRegionResponse> GetRegionAsync(GetRegionRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetRegion", req, GetRegionResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetRegions.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetRegionsResponse> GetRegionsAsync(GetRegionsRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetRegions", req, GetRegionsResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetServiceAccount.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetServiceAccountResponse> GetServiceAccountAsync(GetServiceAccountRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetServiceAccount", req, GetServiceAccountResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetServiceAccounts.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetServiceAccountsResponse> GetServiceAccountsAsync(GetServiceAccountsRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetServiceAccounts", req, GetServiceAccountsResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetUsage.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetUsageResponse> GetUsageAsync(GetUsageRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetUsage", req, GetUsageResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetUser.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetUserResponse> GetUserAsync(GetUserRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetUser", req, GetUserResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetUserGroup.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetUserGroupResponse> GetUserGroupAsync(GetUserGroupRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetUserGroup", req, GetUserGroupResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetUserGroupMembers.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetUserGroupMembersResponse> GetUserGroupMembersAsync(GetUserGroupMembersRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetUserGroupMembers", req, GetUserGroupMembersResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetUserGroups.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetUserGroupsResponse> GetUserGroupsAsync(GetUserGroupsRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetUserGroups", req, GetUserGroupsResponse.Parser, options);
        }

        /// <summary>
        /// Invoke GetUsers.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<GetUsersResponse> GetUsersAsync(GetUsersRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("GetUsers", req, GetUsersResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RemoveUserGroupMember.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RemoveUserGroupMemberResponse> RemoveUserGroupMemberAsync(RemoveUserGroupMemberRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RemoveUserGroupMember", req, RemoveUserGroupMemberResponse.Parser, options);
        }

        /// <summary>
        /// Invoke RenameCustomSearchAttribute.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<RenameCustomSearchAttributeResponse> RenameCustomSearchAttributeAsync(RenameCustomSearchAttributeRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("RenameCustomSearchAttribute", req, RenameCustomSearchAttributeResponse.Parser, options);
        }

        /// <summary>
        /// Invoke SetUserGroupNamespaceAccess.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<SetUserGroupNamespaceAccessResponse> SetUserGroupNamespaceAccessAsync(SetUserGroupNamespaceAccessRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("SetUserGroupNamespaceAccess", req, SetUserGroupNamespaceAccessResponse.Parser, options);
        }

        /// <summary>
        /// Invoke SetUserNamespaceAccess.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<SetUserNamespaceAccessResponse> SetUserNamespaceAccessAsync(SetUserNamespaceAccessRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("SetUserNamespaceAccess", req, SetUserNamespaceAccessResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UpdateAccount.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UpdateAccountResponse> UpdateAccountAsync(UpdateAccountRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UpdateAccount", req, UpdateAccountResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UpdateApiKey.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UpdateApiKeyResponse> UpdateApiKeyAsync(UpdateApiKeyRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UpdateApiKey", req, UpdateApiKeyResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UpdateNamespace.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UpdateNamespaceResponse> UpdateNamespaceAsync(UpdateNamespaceRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UpdateNamespace", req, UpdateNamespaceResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UpdateNamespaceExportSink.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UpdateNamespaceExportSinkResponse> UpdateNamespaceExportSinkAsync(UpdateNamespaceExportSinkRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UpdateNamespaceExportSink", req, UpdateNamespaceExportSinkResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UpdateNexusEndpoint.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UpdateNexusEndpointResponse> UpdateNexusEndpointAsync(UpdateNexusEndpointRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UpdateNexusEndpoint", req, UpdateNexusEndpointResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UpdateServiceAccount.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UpdateServiceAccountResponse> UpdateServiceAccountAsync(UpdateServiceAccountRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UpdateServiceAccount", req, UpdateServiceAccountResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UpdateUser.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UpdateUserResponse> UpdateUserAsync(UpdateUserRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UpdateUser", req, UpdateUserResponse.Parser, options);
        }

        /// <summary>
        /// Invoke UpdateUserGroup.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<UpdateUserGroupResponse> UpdateUserGroupAsync(UpdateUserGroupRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("UpdateUserGroup", req, UpdateUserGroupResponse.Parser, options);
        }

        /// <summary>
        /// Invoke ValidateNamespaceExportSink.
        /// </summary>
        /// <param name="req">Request for the call.</param>
        /// <param name="options">Optional RPC options.</param>
        /// <returns>RPC response</returns>
        public Task<ValidateNamespaceExportSinkResponse> ValidateNamespaceExportSinkAsync(ValidateNamespaceExportSinkRequest req, RpcOptions? options = null)
        {
            return InvokeRpcAsync("ValidateNamespaceExportSink", req, ValidateNamespaceExportSinkResponse.Parser, options);
        }
    }
}