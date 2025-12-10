using System.Text;

using RobotsTxt;

using Xunit;

namespace TestRobotsTxt;

public class TestRobotsMachine
{
    private readonly byte[][] _robotsTxt =
    [
        @"# ROW robots from TAS
# update 08-12-2024 semi configs and login redirect blocked
# Updated on 06-09-2023
# Added Disallow: /*/orders for all bots - SELF-223
# Added Disallow for COAs for google, bing and APAC bots 09-27-2024

User-Agent: *
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /api
Disallow: */jcr:content/
Disallow: /*/search?focus=
Disallow: /*/search/?focus=
Disallow: /*/product/compare?
Disallow: /*/product/compare-
Disallow: /*/orders
Disallow: /*undefined/undefined
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/coa/
Disallow: /certificates/Graphics/COfAInfo/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/COA/Q14/
Disallow: */search/*focus=papers
Disallow: /*/*/life-science/assistant

#Specific allows for chatGPT - note directives apply to both bots
User-agent: GPTBot
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /*/orders
Disallow: /*undefined/undefined
Disallow: */jcr:content/
Disallow: /*/search?focus=
Disallow: /*/search/?focus=
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/life-science/assistant

User-agent: ChatGPT-User
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /*/orders
Disallow: /*undefined/undefined
Disallow: */jcr:content/
Disallow: /*/search?focus=
Disallow: /*/search/?focus=
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/life-science/assistant

User-Agent: Googlebot
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /api
Allow: /api?operation=PricingAndAvailability
Disallow: */jcr:content/
Disallow: /*/search?focus=
Disallow: /*/search/?focus=
Disallow: /*/product/compare?
Disallow: /*/product/compare-
Disallow: /*/orders
Disallow: /*undefined/undefined
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/coa/
Disallow: /certificates/Graphics/COfAInfo/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/COA/Q14/
Disallow: */search/*focus=papers
Disallow: /*/*/life-science/assistant

# added 03-20-2024
User-Agent: Bingbot
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /api
Allow: /api?operation=PricingAndAvailability
Disallow: */jcr:content/
Disallow: /*/search?focus=
Disallow: /*/search/?focus=
Disallow: /*/product/compare?
Disallow: /*/product/compare-
Disallow: /*/orders
Disallow: /*undefined/undefined
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/coa/
Disallow: /certificates/Graphics/COfAInfo/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/COA/Q14/
Disallow: /*/*/life-science/assistant

User-Agent: Botify
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /api
Allow: /api?operation=PricingAndAvailability
Disallow: */jcr:content/
Disallow: /*/search?focus=
Disallow: /*/search/?focus=
Disallow: /*/product/compare?
Disallow: /*/product/compare-
Disallow: /*/orders
Disallow: /*undefined/undefined
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/life-science/assistant

User-Agent: Adsbot-Google
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /api
Disallow: */jcr:content/
Disallow: /*/search?focus=
Disallow: /*/search/?focus=
Disallow: /*/product/compare?
Disallow: /*/product/compare-
Disallow: /*/orders
Disallow: /*undefined/undefined
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/life-science/assistant

# APAC Bots
# China
User-Agent: Baiduspider
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: /api
Disallow: */jcr:content/
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /*/search?focus=
Disallow: /*/search/?focus=
Disallow: /*/product/compare?
Disallow: /*/product/compare-
Disallow: /*/orders
Disallow: /*undefined/undefined
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/coa/
Disallow: /certificates/Graphics/COfAInfo/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/COA/Q14/
Disallow: /*/*/life-science/assistant

User-agent: Sosospider
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: */jcr:content/
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /api
Disallow: /*/search?focus=
Disallow: /*/search/?focus=
Disallow: /*/product/compare?
Disallow: /*/product/compare-
Disallow: /*/orders
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/coa/
Disallow: /certificates/Graphics/COfAInfo/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/COA/Q14/
Disallow: /*/*/life-science/assistant

User-agent: Sogou spider
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: */jcr:content/
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /api
Disallow: /*/search?focus=
Disallow: /*/search/?focus=
Disallow: /*/product/compare?
Disallow: /*/product/compare-
Disallow: /*/orders
Disallow: /*undefined/undefined
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/coa/
Disallow: /certificates/Graphics/COfAInfo/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/COA/Q14/
Disallow: /*/*/life-science/assistant

User-agent: Sogou+spider
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: */jcr:content/
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /api
Disallow: /*/search?focus=
Disallow: /*/search/?focus=
Disallow: /*/product/compare?
Disallow: /*/product/compare-
Disallow: /*/orders
Disallow: /*undefined/undefined
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/coa/
Disallow: /certificates/Graphics/COfAInfo/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/COA/Q14/
Disallow: /*/*/life-science/assistant

User-agent: YoudaoBot
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: */jcr:content/
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /api
Disallow: /*/search?focus=
Disallow: /*/search/?focus=
Disallow: /*/product/compare?
Disallow: /*/product/compare-
Disallow: /*/orders
Disallow: /*undefined/undefined
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/coa/
Disallow: /certificates/Graphics/COfAInfo/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/COA/Q14/
Disallow: /*/*/life-science/assistant

# Naverbot - Korea
User-agent: Yeti
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: */jcr:content/
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /api
Disallow: /*/search?focus=
Disallow: /*/search/?focus=
Disallow: /*/product/compare?
Disallow: /*/product/compare-
Disallow: /*/orders
Disallow: /*undefined/undefined
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/coa/
Disallow: /certificates/Graphics/COfAInfo/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/COA/Q14/
Disallow: /*/*/life-science/assistant

# Daum
User-agent: DAUM
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: */jcr:content/
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /api
Disallow: /*/search?focus=
Disallow: /*/search/?focus=
Disallow: /*/product/compare?
Disallow: /*/product/compare-
Disallow: /*/orders
Disallow: /*undefined/undefined
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/coa/
Disallow: /certificates/Graphics/COfAInfo/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/COA/Q14/
Disallow: /*/*/life-science/assistant

# Yandex
# Added gc fb sid id redirect param clean 12/13/2021
User-agent: YandexBot
Allow: /
Disallow: /*/semi-configurators/sirna?term=
Disallow: /*/semi-configurators/shrna?term=
Disallow: */jcr:content/
Disallow: login?redirect
Disallow: /*/login?redirect
Disallow: /api
Disallow: /*/search/?focus=
Disallow: /*/search?focus=
Disallow: /*/product/compare?
Disallow: /*/product/compare-
Disallow: /*/orders
Disallow: /*undefined/undefined
Disallow: /*/*/life-science/quality-and-regulatory-management/m-clarity-program
Disallow: /*/*/services/support/bulk-quotation-request
Disallow: /*/*/coa/
Disallow: /certificates/Graphics/COfAInfo/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/
Disallow: /certificates/sapfs/PROD/sap/certificate_pdfs/COA/Q14/
Disallow: /*/*/life-science/assistant

Clean-param: redirect /*/login
Clean-param: gc /*
Clean-param: fb /*
Clean-param: redirect /*
Clean-param: sid /*
Clean-param: id /*

# Added 11-03-2022
User-agent: PetalBot
Disallow: /

User-agent: ConveraCrawler
Disallow: /

User-agent: DotBot
Disallow: /

User-agent: ingenieur
Disallow: /

User-agent: Mail.Ru
Disallow: /

User-agent: JikeSpider
Disallow: /

User-agent: EasouSpider
Disallow: /

User-agent: YisouSpider
Disallow: /

Sitemap: https://www.sigmaaldrich.com/sitemap_index.xml
"u8.ToArray(),
        @"User-agent: *
Disallow: /account/
Disallow: /adRedir.do*
Disallow: /ads/
Disallow: /b2b/
Disallow: /billboard/
Disallow: /cart/
Disallow: /catalog/browseCatalog.do*
Disallow: /catalogrequest/
Disallow: /catalog/search.do*
Disallow: /checkout/
Disallow: /common/
Disallow: /compare/
Disallow: /contracts/
Disallow: /csl/
Disallow: /customerservice/
Disallow: /default/
Disallow: /employeepurchase
Disallow: /employeepurchases.do*
Disallow: /epp
Disallow: /examples/
Disallow: /inkTonerManuf.do*
Disallow: /internal/
Disallow: /mb/search.do*
Disallow: /mb/stores/list.do*
Disallow: /mb/wifiConnect.do*
Disallow: /mb/cart.do*
Disallow: /orderhistory/
Disallow: /printconfigurator/
Disallow: /promo/
Disallow: /qp/
Disallow: /shop/
Disallow: /storelocator/wifiConnect.do*
Disallow: /stores/wifiConnect.do*
Disallow: /tealeaf/
Disallow: /textSearch.do*
Disallow: /txtSearchDD.do*
Disallow: /userprofile/
Disallow: /vendor/
Disallow: /workflow/
Disallow: /select
Disallow: /businessrewards/
Disallow: /ccpa/lookup.do*
Disallow: /a/search/
Disallow: /b/widget/
Disallow: /b/*/*/*/*/N-*
Disallow: /b/clearance/
Allow: /b/clearance/Featured_Items--Clearance/clearance
Sitemap: https://www.example.com/sitemap.xml"u8.ToArray(),
    ];

    [Theory]
    [InlineData(0,
        "/US/en/search/7423-31-6?focus=papers&page=1&perpage=30&sort=relevance&term=7423-31-6&type=citation_search",
        false)]
    [InlineData(1, "/", true)]
    public void Test1(int index, string path, bool expected)
    {
        var machine = new RobotsMachine(_robotsTxt[index],
            ["botify"u8.ToArray(), "googlebot"u8.ToArray(),]);
        var actual = machine.PathAllowedByRobots(Encoding.UTF8.GetBytes(path));
        Assert.Equal(expected, actual);
    }
}
