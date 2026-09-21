import {catalogPageStyle, getCatalogStyle} from "../../services/theme";
import React from "react";
import CatalogPage from "../CatalogPage";
import AdBanner from "../ad/adBanner";
import Head from "next/head";
import Theme2016 from "../theme2016";
import CatalogPageInput from "../catalogPageInput";
import CatalogPageNavigation from "../catalogPageNavigation";
import CatalogFilters from "../catalogFilters";
import CatalogLegend from "../catalogLegend";
import CatalogPageResults from "../catalogPageResults";
import CatalogPageStore from "../../stores/catalogPage";
import CatalogPageStore2018 from "../../components/CatalogPage/stores/CatalogPageStore";
import {createUseStyles} from "react-jss";
import AdSkyscraper from "../ad/adSkyscraper";

const useStyles = createUseStyles({
    title: {
        fontWeight: 700,
        fontSize: '32px',
        marginBottom: '12px',
        color: 'var(--text-color-primary)',
    },
    catalogContainer: {
        backgroundColor: 'var(--white-color)',
        padding: '2px 4px',
    },
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

const AssetPage = () => {
    const s = useStyles();

    if (getCatalogStyle() === catalogPageStyle.Modern)
        return <CatalogPageStore2018.Provider>
            <Theme2016>
                <Head>
                    <title>Catalog - Averia</title>
                </Head>

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
        </CatalogPageStore2018.Provider>

    return <CatalogPageStore.Provider>
        <div className='container mt-4 ssp'>
            <AdBanner/>
            <div className={s.catalogContainer}>
                <div className='row mt-2'>
                    <div className='col-12 col-md-4 col-lg-2'>
                        <h1 className={s.title}>Catalog</h1>
                    </div>
                    <div className='col-12 col-md-8 col-lg-10'>
                        <CatalogPageInput/>
                    </div>
                </div>
                <div className='row'>
                    <div className='col-12 col-md-4 col-lg-2'>
                        <div className='divider-right'>
                            <div className='pe-2'>
                                <CatalogPageNavigation/>
                                <CatalogFilters/>
                                <CatalogLegend/>
                            </div>
                        </div>
                    </div>
                    <div className='col-12 col-md-8 col-lg-10'>
                        <CatalogPageResults/>
                    </div>
                </div>
            </div>
        </div>
    </CatalogPageStore.Provider>
}

export default AssetPage;