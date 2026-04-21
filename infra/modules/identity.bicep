// Identity module — manual CIAM / app registration wiring only.
// Does NOT provision Entra External ID or app registrations.
// It only surfaces the externally created values to the deployment graph.

param externalTenantId string = ''
param apiClientId string = ''
param mobileClientId string = ''
param authority string = ''

output externalTenantId string = externalTenantId
output apiClientId string = apiClientId
output mobileClientId string = mobileClientId
output authority string = authority
