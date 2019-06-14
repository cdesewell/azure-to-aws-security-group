using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;

namespace AzureIpAddressSecurityGroupGenerator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            //Downloaded from https://www.microsoft.com/en-us/download/details.aspx?id=41653
            XmlSerializer xml = new XmlSerializer(typeof(AzurePublicIpAddresses));
            FileStream xmlStream = new FileStream("PublicIPs_20190612.xml", FileMode.Open);
            var publicAddresses = (AzurePublicIpAddresses)xml.Deserialize(xmlStream);
            
            var europeWestIpAddresses = publicAddresses.Region.Single(r => r.Name == "europewest");

            List<Permission> permissions = new List<Permission>
            {
                new Permission
                {
                    Name = "MS SQL",
                    IpProtocol = "TCP",
                    FromPort = 1433,
                    ToPort = 1433
                },
                new Permission
                {
                    Name = "HTTP",
                    IpProtocol = "TCP",
                    FromPort = 80,
                    ToPort = 80,
                },
                new Permission
                {
                    Name = "HTTPS",
                    IpProtocol = "TCP",
                    FromPort = 443,
                    ToPort = 443,
                }
            };

            foreach (var permission in permissions)
            {
                await GrantAccess(permission, europeWestIpAddresses);
            }
        }

        private static async Task GrantAccess(Permission permission, Region region)
        {
            //Specify a credentials
            var aws = new AmazonEC2Client("", "",RegionEndpoint.EUWest1);

            var batchCount = 0;
            foreach (var ipRangeBatch in region.IpRange.Batch(50))
            {
                var securityGroupCreateRequest = new CreateSecurityGroupRequest
                {
                    //Specify a VPC
                    VpcId = "",

                    GroupName = $"Azure {permission.Name} {batchCount}",
                    Description =
                        $"An auto generated security group to allow azure {region.Name} datacenters access to {permission.Name}"
                };
                var securityGroupCreateResponse = await aws.CreateSecurityGroupAsync(securityGroupCreateRequest);

                var ingressRequest = new AuthorizeSecurityGroupIngressRequest
                {
                    GroupId = securityGroupCreateResponse.GroupId,
                    IpPermissions = ipRangeBatch.Select(ip => new IpPermission
                    {
                        IpProtocol = permission.IpProtocol,
                        FromPort = permission.FromPort,
                        ToPort = permission.ToPort,
                        IpRanges = new List<string> { ip.Subnet }
                    }).ToList()
                };

                var ingressResponse = await aws.AuthorizeSecurityGroupIngressAsync(ingressRequest);
                batchCount++;
            }
        }
    }



    public class AzurePublicIpAddresses
    {
        [XmlElement(ElementName = "Region")]
        public Region[] Region { get; set; }
    }

    public class Region
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlElement(ElementName = "IpRange")]
        public IpRange[] IpRange
        {
            get;
            set;
        }
    }

    public class IpRange
    {
        [XmlAttribute]
        public string Subnet { get; set; }
    }

    public class Permission
    {
        public string Name { get; set; }
        public string IpProtocol { get; set; }
        public int FromPort { get; set; }
        public int ToPort { get; set; }
    }
}
