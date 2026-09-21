import { createUseStyles } from "react-jss";
import CatalogPageStore from "../../components/CatalogPage/stores/CatalogPageStore";
import Theme2016 from "../../components/theme2016";
import AdBanner from "../../components/ad/adBanner";
import AdSkyscraper from "../../components/ad/adSkyscraper";
import CatalogPage from "../../components/CatalogPage";

const useStyles = createUseStyles({
    bannerClass: {
        marginBottom: 15,
    },
    adSkyscraper: {
        marginTop: 115,
        '@media (max-width: 1532px)': {
            display: 'none',
        },
    },
    container: {
        '@media (min-width: 1532px)': {
            maxWidth: '1154px!important',
        },
    },
})

const ModernCatalogPage = () => {
    const s = useStyles();

    return <CatalogPageStore.Provider>
            <Theme2016>
                <div className={s.container + ' container padding-none flex justify-content-between'}>
                    <div className="container flex flex-column padding-none margin-none">
                        <AdBanner className={s.bannerClass}/>
                        <CatalogPage/>
                    </div>
                    <div className={`flex padding-none ${s.adSkyscraper}`}>
                        <AdSkyscraper className={s.bannerClass}/>
                    </div>
                </div>
            </Theme2016>
        </CatalogPageStore.Provider>
}

ModernCatalogPage.getInitialProps = () => {
    return {
        title: 'Catalog - Averia',
    }
}

export default ModernCatalogPage;
