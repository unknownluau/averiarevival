import {createUseStyles} from "react-jss";
import Selector from "../../selector";
import CatalogPageStore, {
    CatalogCategory,
    CatalogSortBy, CatalogSubCategory,
    EnumToString, FormatCamelCase,
    SortByToString
} from "../stores/CatalogPageStore";
import CatalogItemCard from "./CatalogItemCard";
import {useEffect, useRef, useState} from "react";
import ActionButton from "../../actionButton";
import useButtonStyles from "../../../styles/buttonStyles";
import {getTheme, themeType} from "../../../services/theme";

const useStyles = createUseStyles({
    resultsWrapper: {
    },
    breadcrumbsContainer: {
        margin: "6px 0 12px",
        paddingLeft: 6,
        "@media(max-width: 576px)": {
            "& > div": {
                display: "flex",
                justifyContent: "center!important",
                marginBottom: 6,
            },
            "& div:last-child": {
                marginBottom: 0,
            }
        },
    },
    selectorWrapper: {
        width: 230,
    },
    selector: {
        padding: "5px 12px",
        lineHeight: "18px",
    },
    selectorOption: {
        // padding: "5px 12px",
        // lineHeight: "18px",
    },
    resultsContainer: {
        minWidth: 60,
        gap: 9.6,
    },
    paginationContainer: {
        display: 'flex',
        width: '100%',
        justifyContent: 'center',
        gap: 10,
        alignItems: 'center',
        marginTop: 25
    },
    paginationBtn: {
        aspectRatio: '1 / 1',
        padding: 3,
        display: 'flex',
        borderColor: p => p.theme !== themeType.dark ? 'var(--text-color-secondary)' : 'transparent!important',
        '& span': {
            backgroundSize: '48px auto',
            height: 24,
            width: 24,
            backgroundImage: "url(/img/generic_03112016.svg)",
            backgroundRepeat: "no-repeat",
            display: 'inline-block',
            verticalAlign: 'middle',
            filter: p => p.theme === themeType.dark ? 'invert(1)' : 'none',
        },
        "&.disabled": {
            filter: p => p.theme === themeType.dark ? 'invert(1)' : 'none',
        },
    },
    pages: {
        wordSpacing: '0.25em',
    },
    backIcon: {
        backgroundPosition:'0 -360px!important',
    },
    forwardIcon: {
        backgroundPosition:'0 -336px!important',
    },
});

function CatalogResults() {
    const s = useStyles({theme: getTheme()});
    const buttonStyles = useButtonStyles();
    // hierarchy iof breadcrumbs: category > subcategory > keyword
    const store = CatalogPageStore.useContainer();
    const deb = useRef(false)
    const [currentPage, setCurrentPage] = useState(1);
    const [totalPages, setTotalPages] = useState(1);

    useEffect(() => {
        setCurrentPage(store?.resultMetadata?.page || 1);
        setTotalPages(store.total !== 0 && store?.resultMetadata?.limit !== 0 ? Math.max(1, Math.ceil(store.total / store.resultMetadata.limit)) : 1);
    }, [store.total, store.resultMetadata, store.isLoading]);

    return <div className={s.resultsWrapper}>
        <div className={`${s.breadcrumbsContainer} flex flex-column w-100`}>
            <div style={{ marginBottom: 6, display: 'flex', gap: 5 }}>
                <span>{FormatCamelCase(EnumToString(store.options.category, CatalogCategory)) || "N/A"}</span>
                {`>`}
                <span>{FormatCamelCase(EnumToString(store.options.subCategory, CatalogSubCategory)) || "N/A"}</span>
            </div>
            <div style={{ color: "#757575", fontSize: 12, fontWeight: 400, }}>
                <span style={{ marginTop: 2, }}>{currentPage} - {totalPages} of {store.total > 0 ? store.total : store.results.length} Results</span>
            </div>
            <div className="w-100 flex justify-content-end">
                <Selector
                    options={Object.keys(CatalogSortBy).map(d => ({
                        name: SortByToString(CatalogSortBy[d]),
                        value: CatalogSortBy[d] || 0,
                    }))}
                    onChange={async newValue => {
                        if (store.isLoading || store.options.sortBy === newValue.value) return false;
                        const ok = await store.RefreshCatalogItems(null, true, {sortBy: newValue.value});
                        if (ok) {
                            store.setOptions({...store.options, sortBy: newValue.value});
                            return true;
                        }
                        return false;
                    }}
                    wrapperClass={s.selectorWrapper}
                    selectorOptionClass={s.selectorOption}
                    className={s.selector}
                />
            </div>
        </div>
        <div className={`${s.resultsContainer} flex`}>
            {
                store.isLoading && store.results.length === 0 ?
                    <span className="spinner" style={{ backgroundSize: "auto 36px" }}/>
                    :
                    store.results.length === 0 ?
                        <div className="section-content-off w-100">No results found</div>
                        :
                        store.results.map(item => {
                            return <CatalogItemCard item={item} key={item.id} />
                        })
            }
        </div>
        <div className={`${s.paginationContainer}`}>
            <ActionButton
                className={`${s.paginationBtn} ${(store.results.length === 0 || store?.resultMetadata?.prevCursor == null) ? 'disabled' : ''}`}
                buttonStyle={(store.results.length === 0 || store?.resultMetadata?.prevCursor == null) ? buttonStyles.newDisabledCancelButton : buttonStyles.newCancelButton}
                onClick={async e => {
                    e.preventDefault();
                    if (deb.current || store.isLoading || store?.resultMetadata?.prevCursor == null) {
                        return
                    }
                    deb.current = true
                    await store.RefreshCatalogItems(null, false, {}, store.creatorOption, store.resultMetadata.prevCursor)
                    deb.current = false
                }}
            >
                <span className={s.icon + ' ' + s.backIcon} />
            </ActionButton>
            <span className={s.pages}>
                {currentPage} / {totalPages}
            </span>
            <ActionButton
                className={`${s.paginationBtn} ${(store.results.length === 0 || store?.resultMetadata?.nextCursor == null) ? 'disabled' : ''}`}
                buttonStyle={(store.results.length === 0 || store?.resultMetadata?.nextCursor == null) ? buttonStyles.newDisabledCancelButton : buttonStyles.newCancelButton}
                onClick={async e => {
                    e.preventDefault();
                    if (deb.current || store.isLoading || store?.resultMetadata?.nextCursor == null) {
                        return
                    }
                    deb.current = true
                    await store.RefreshCatalogItems(null, false, {}, store.creatorOption, store.resultMetadata.nextCursor)
                    deb.current = false
                }}
            >
                <span className={s.icon + ' ' + s.forwardIcon} />
            </ActionButton>
        </div>
    </div>
}

export default CatalogResults;
