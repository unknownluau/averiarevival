import { createUseStyles } from "react-jss";
import { useEffect, useRef, useState } from "react";
import NewLink from "../../NewLink";
import { wait } from "../../../lib/utils";
import CatalogPageStore from "../stores/CatalogPageStore";
import ActionButton from "../../actionButton";
import useButtonStyles from "../../../styles/buttonStyles";
import {getTheme, themeType} from "../../../services/theme";

const useStyles = createUseStyles({
    searchOptionWrapper: {
        borderBottom: "1px solid #b8b8b8",
        margin: "0 12px 0 0",
        "@media(max-width: 576px)": {
            margin: "0 6px",
        },
    },
    searchOptionHeader: {
        fontSize: 20,
        fontWeight: 700,
        padding: "5px 0",
        lineHeight: "1em",
    },
    searchOptionHeaderContainer: {
        marginBottom: 4,
        "@media(max-width: 576px)": {
            justifyContent: "space-between",
            alignItems: "center",
            marginTop: 4,
        },
    },
    categoryWrapper: {
        paddingBottom: 12,
    },
    categoryContainer: {
        marginBottom: 6,
        "& a": {
            fontSize: 12,
            fontWeight: 400,
        },
    },
    subCategoryContainer: {
        paddingLeft: 12,
    },
    subCategoryWrapper: {
        fontSize: 16,
        lineHeight: "1.4em",
        "& > a": {
            display: "inline-flex",
            justifyContent: "space-between",
            alignItems: "center",
            width: "100%",
        },
    },
    collapse: {
        height: 0,
        // display: "none",
        flexDirection: "column",
        transition: "height .35s ease",
        overflow: "hidden",
        "&.in": {
            display: "flex",
            height: "auto",
        },
        "&.out": {
            display: "flex",
            height: 0,
        },
    },
    btnParent: {
        height: 22,
        width: 22,
        marginBottom: 6,
        display: "none!important",
        "@media(max-width: 576px)": {
            display: "flex!important",
        },
    },
    btn: {
        height: 22,
        width: 22,
        border: "none!important",
        background: "none!important",
        padding: 1,
        "& span": {
            width: 20,
            height: 20,
            backgroundPosition: "0 -420px",
            backgroundSize: "40px auto",
        },
    },
});

function CatalogNavigation() {
    const s = useStyles();
    const store = CatalogPageStore.useContainer();
    const buttonStyles = useButtonStyles();
    
    const { categoryNav } = store;
    const locked = useRef(false);
    const [transition, setTransition] = useState(false);
    // a category can be open if here or if its currently selected in the store
    const [openedCategory, setOpenCategory] = useState(null);
    const [closingCategory, setClosingCategory] = useState(null);

    const [deb, setDeb] = useState(false);

    useEffect(() => {
        let cancelled = false;

        async function run() {
            await wait(0.4);
            if (!cancelled) setClosingCategory(null);
        }

        run().then();

        return () => {
            cancelled = true;
        };
    }, [closingCategory]);
    
    return <div className={`${s.categoryWrapper} ${s.searchOptionWrapper} flex flex-column`}>
        <div className={`flex ${s.searchOptionHeaderContainer}`}>
            <h3 className={s.searchOptionHeader}>Category</h3>
            <ActionButton
                divClassName={s.btnParent}
                className={`${s.btn}`}
                buttonStyle={buttonStyles.newCancelButton}
                onClick={e => {
                    e.preventDefault();
                    if (deb) return;
                    setDeb(true);
                    store.setNavVisible(!store.navVisible);
                    wait(0.35).then(() => setDeb(false));
                }}
            >
                <div className="flex justify-content-center align-items-center">
                    <span className="icon-close"/>
                </div>
            </ActionButton>
        </div>
        <div>
            {
                categoryNav?.map(cat => {
                    return <div className={`${s.categoryContainer} flex flex-column`} id={cat.categoryId.toString()}>
                        <div className={s.subCategoryWrapper}>
                            <NewLink className={getTheme() === themeType.dark ? 'link2019' : 'link2019-gray'} style={store.options.category === cat.categoryId || openedCategory === cat.categoryId ? {color: "var(--primary-color)!important"} : {}} onClick={async e => {
                                e.preventDefault();
                                if (locked.current || store.refreshDebounce.current || store.options.category === cat.categoryId) return;
                                locked.current = true;
                                if (cat.subCategories.length === 0) {
                                    store.setOptions({...store.options, category: cat.categoryId});
                                    store.setOptions({...store.options, subCategory: null});
                                    store.RefreshCatalogItems(null, true, {category: cat.categoryId});
                                    await wait(1);
                                    locked.current = false;
                                    return;
                                }
                                
                                let oldOpenCat = openedCategory === cat.categoryId && store.options.category !== cat.categoryId ? openedCategory : null;
                                setOpenCategory(openedCategory === cat.categoryId && store.options.category !== cat.categoryId ? null : cat.categoryId);
                                setClosingCategory(oldOpenCat);
                                setTransition(true);
                                await wait(0.375);
                                setTransition(false);
                                locked.current = false;
                            }}>
                                <span className="inherit-color inherit-font-size">{cat.name}</span>
                                <span
                                    className={`${cat.subCategories.length === 0 ? "display-none" : ""} inherit-color inherit-font-size ${(openedCategory === cat.categoryId || store.options.category === cat.categoryId) && getTheme() === themeType.dark && "invert-color"} ${openedCategory === cat.categoryId || store.options.category === cat.categoryId ? "icon-minus" : "icon-plus"}`}/>
                            </NewLink>
                        </div>
                        
                        <div className={`
                        ${s.subCategoryContainer}
                        ${s.collapse}
                        ${openedCategory === cat.categoryId || store.options.category === cat.categoryId ? "in" : ""}
                        ${transition && closingCategory === cat.categoryId ? "out" : ""}
                        ${cat.subCategories.length === 0 ? "display-none" : ""}
                        `}
                             style={transition && openedCategory === cat.categoryId ? { height: cat.subCategories.length * 22.4 } : {}}
                        >
                            {
                                cat.subCategories.map(sub => {
                                    return <div className={s.subCategoryWrapper}>
                                        <NewLink className={`link2019-gray`} style={store.options.category === cat.categoryId && store.options.subCategory === sub.subCategoryId ? {color: "var(--primary-color)!important"} : {}} onClick={async e => {
                                            e.preventDefault();
                                            if (locked.current || store.refreshDebounce.current || (store.options.category === cat.categoryId && store.options.subCategory === sub.subCategoryId)) return;
                                            locked.current = true;

                                            store.setOptions({...store.options, category: cat.categoryId, subCategory: sub.subCategoryId});
                                            await store.RefreshCatalogItems(null, true, {
                                                category: cat.categoryId,
                                                subCategory: sub.subCategoryId
                                            });

                                            locked.current = false;
                                        }}>{sub.name?.replace("Collectible Accessories", "Collect. Accessories") || sub.subCategory?.replace("Collectible Accessories", "Collect. Accessories")}</NewLink>
                                    </div>
                                })
                            }
                        </div>
                    </div>
                })
            }
        </div>
    </div>
}

export default CatalogNavigation;
