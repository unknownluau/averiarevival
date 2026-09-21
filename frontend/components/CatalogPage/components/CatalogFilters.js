import {createUseStyles} from "react-jss";
import CatalogPageStore from "../stores/CatalogPageStore";
import useButtonStyles from "../../../styles/buttonStyles";
import { useRef } from "react";
import { tick, wait } from "../../../lib/utils";
import ActionButton from "../../actionButton";

const useStyles = createUseStyles({
    searchOptionWrapper: {
        borderBottom: "1px solid #b8b8b8",
        margin: "0 12px 0 0",
        paddingBottom: 12,
        "@media(max-width: 576px)": {
            margin: "0 6px",
        },
    },
    searchOptionHeader: {
        fontSize: 20,
        fontWeight: 700,
        padding: "12px 0 0",
        lineHeight: "1em",
        margin: 0,
    },
    filterHeader: {
        padding: "9px 0 0",
        fontSize: 16,
        fontWeight: 500,
        margin: 0,
    },
    filterContainer: {
        padding: "0 12px 0 0",
    },
    priceContainer: {
        marginTop: 6,
        "& > label": {
            fontSize: 12,
            lineHeight: "1em",
        },
    },
    genreCheckbox: {
        marginTop: 2,
        fontSize: 16,
        fontWeight: 500,
        lineHeight: "1.4em",
        "& *": {
            fontSize: 12,
            fontWeight: 400,
        },
    },
    allGenres: {
        fontSize: 12,
        fontWeight: 400,
        lineHeight: "2.4em",
        color: "var(--text-color-primary)",
        "&:hover": {
            textDecoration: "underline!important",
        },
    },
    goButton: {
        margin: 0,
        marginTop: 6,
        padding: 4,
        fontSize: 14,
        lineHeight: "1em",
        fontWeight: 500,
    },
    inputText: {
        padding: "0 5px",
        fontSize: 12,
        lineHeight: "1.4em",
        height: 24,
        "&:first-child": {
            marginBottom: 6,
        },
    },
});

function CatalogFilters() {
    const s = useStyles();
    const store = CatalogPageStore.useContainer();
    const buttonStyles = useButtonStyles();
    
    const locked = useRef(false);
    const { genreNav, options } = store;
    
    return <div className={s.searchOptionWrapper}>
        <h3 className={s.searchOptionHeader}>Filters</h3>
        <div>
            <h5 className={s.filterHeader}>Genre</h5>
            <a className={s.allGenres} onClick={async e => {
                
                if (locked.current || store.refreshDebounce.current) return;
                locked.current = true;
                
                let clone = options.genres.Clone();
                clone.Clear();
                // clone.Add(genreNav.find(g => g.genre.toLowerCase() === "all").genreId);
                store.setOptions({...options, genres: clone});
                await tick();
                store.RefreshCatalogItems(null, true, { genres: clone });
                
                await wait(0.75);
                locked.current = false;
            }}>All Genres</a>
            <div>
                {
                    genreNav.map(genre => {
                        if (genre.genre.toLowerCase() === "all") return null;
                        return <div className={`${s.genreCheckbox} checkbox2019`} key={genre.genreId.toString()}>
                            <input
                                checked={options.genres.Contains(genre.genreId)}
                                type="checkbox"
                                id={`genre-${genre.genreId}`}
                                onClick={async e => {
                                    
                                    if (locked.current || store.refreshDebounce.current) return;
                                    locked.current = true;
                                    
                                    let genres;
                                    if (!options.genres.Contains(genre.genreId)) { // add
                                        genres = store.AddGenre(genre.genreId);
                                    } else { // remove
                                        genres = store.RemoveGenre(genre.genreId);
                                    }
                                    store.setOptions({...options, genres: genres});
                                    await tick();
                                    store.RefreshCatalogItems(null, true, { genres: genres });
                                    
                                    await wait(0.75);
                                    locked.current = false;
                                }}
                            />
                            <label htmlFor={`genre-${genre.genreId}`}
                            >{genre.name || genre.genre}</label>
                        </div>
                    })
                }
            </div>
        </div>
        <div>
            <h5 className={s.filterHeader}>Creator</h5>
            <div className={s.priceFilters}>
                <div className={`${s.priceContainer} radio2019`}>
                    <input
                        id="creator-1"
                        type="radio"
                        onChange={async e => {
                            
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;

                            store.setCreatorOption(1);
                            await tick();
                            store.RefreshCatalogItems(null, true, {}, 1);
                            
                            await wait(0.75);
                            locked.current = false;
                        }}
                        checked={store.creatorOption === 1}
                    />
                    <label htmlFor="creator-1">All Creators</label>
                </div>
                <div className={`${s.priceContainer} radio2019`}>
                    <input
                        id="creator-2"
                        type="radio"
                        onChange={async e => {
                            
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;

                            store.setCreatorOption(2);
                            await tick();
                            store.RefreshCatalogItems(null, true, {}, 2);
                            
                            await wait(0.75);
                            locked.current = false;
                        }}
                        checked={store.creatorOption === 2}
                    />
                    <label htmlFor="creator-2">ROBLOX</label>
                </div>
                <div className={`${s.priceContainer} radio2019`}>
                    <input
                        id="creator-3"
                        type="radio"
                        onChange={async e => {
                            
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;

                            store.setCreatorOption(3);
                            await tick();
                            store.RefreshCatalogItems(null, true, {}, 3);
                            
                            await wait(0.75);
                            locked.current = false;
                        }}
                        checked={store.creatorOption === 3}
                    />
                    <label htmlFor="creator-3">
                        <input
                            type="text"
                            className={`inputTextStyle ${s.inputText}`}
                            placeholder="Username"
                            pattern="^[a-zA-Z0-9_ ]{0,100}$"
                            name="creatorName"
                            maxLength={100}
                            onSelect={_ => {
                                if (store.creatorOption!==3) store.setCreatorOption(3);
                            }}
                            onChange={e => store.setCreator(e.target.value)}
                        />
                        <ActionButton className={`${s.goButton} ${s.goButton} ${store.creatorOption !== 3 ? "disabled" : ""}`}
                                      buttonStyle={buttonStyles.newContinueButton} label="Go" onClick={async e => {
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;
                            store.RefreshCatalogItems(null, true);
                            await wait(0.75);
                            locked.current = false;
                        }}/>
                    </label>
                </div>
            </div>
        </div>

        <div>
            <h5 className={s.filterHeader}>Creator Type</h5>
            <div className={s.priceFilters}>
                <div className={`${s.priceContainer} radio2019`}>
                    <input
                        id="creator-type-user"
                        type="radio"
                        onChange={async e => {
                            
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;

                            store.setCreatorType(1);
                            await tick();
                            store.RefreshCatalogItems(null, true, { creatorType: 1 });

                            await wait(0.75);
                            locked.current = false;
                        }}
                        checked={store.creatorType===1}
                    />
                    <label htmlFor="creator-type-user">User</label>
                </div>
                <div className={`${s.priceContainer} radio2019`}>
                    <input
                        id="creator-type-group"
                        type="radio"
                        onChange={async e => {
                            
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;

                            store.setCreatorType(2);
                            await tick();
                            store.RefreshCatalogItems(null, true, { creatorType: 2 });

                            await wait(0.75);
                            locked.current = false;
                        }}
                        checked={store.creatorType===2}
                    />
                    <label htmlFor="creator-type-group">Group</label>
                </div>
            </div>
        </div>

        <div>
            <h5 className={s.filterHeader}>Currency</h5>
            <div className={s.priceFilters}>
                <div className={`${s.priceContainer} radio2019`}>
                    <input
                        id="currency-3"
                        type="radio"
                        onChange={async e => {
                            
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;

                            store.setOptions({...options, selectedCurrency: 3});
                            await tick();
                            store.RefreshCatalogItems(null, true, { selectedCurrency: 3 });

                            await wait(0.75);
                            locked.current = false;
                        }}
                        checked={options.selectedCurrency === 3}
                    />
                    <label htmlFor="currency-3">Any</label>
                </div>
                <div className={`${s.priceContainer} radio2019`}>
                    <input
                        id="currency-1"
                        type="radio"
                        onChange={async e => {
                            
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;

                            store.setOptions({...options, selectedCurrency: 1});
                            await tick();
                            store.RefreshCatalogItems(null, true, { selectedCurrency: 1 });
                            
                            await wait(0.75);
                            locked.current = false;
                        }}
                        checked={options.selectedCurrency === 1}
                    />
                    <label htmlFor="currency-1">Robux</label>
                </div>
                <div className={`${s.priceContainer} radio2019`}>
                    <input
                        id="currency-2"
                        type="radio"
                        onChange={async e => {
                            
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;

                            store.setOptions({...options, selectedCurrency: 2});
                            await tick();
                            store.RefreshCatalogItems(null, true, { selectedCurrency: 2 });
                            
                            await wait(0.75);
                            locked.current = false;
                        }}
                        checked={options.selectedCurrency === 2}
                    />
                    <label htmlFor="currency-2">Tickets</label>
                </div>
            </div>
        </div>
        <div>
            <h5 className={s.filterHeader}>Price</h5>
            <div className={s.priceFilters}>
                <div className={`${s.priceContainer} radio2019`}>
                    <input
                        id="priceOption-0"
                        type="radio"
                        onChange={async e => {
                            
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;

                            store.setPriceOption(0);
                            await tick();
                            store.RefreshCatalogItems(null, true, { priceOption: 0 });
                            
                            await wait(0.75);
                            locked.current = false;
                        }}
                        checked={store.priceOption === 0}
                    />
                    <label htmlFor="priceOption-0">Any Price</label>
                </div>
                <div className={`${s.priceContainer} radio2019`}>
                    <input
                        id="priceOption-1"
                        type="radio"
                        onChange={async e => {
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;

                            store.setPriceOption(1);
                            await tick();
                            store.RefreshCatalogItems(null, true, { priceOption: 1 });
                            
                            await wait(0.75);
                            locked.current = false;
                        }}
                        checked={store.priceOption === 1}
                    />
                    <label htmlFor="priceOption-1">
                        <input
                            type="number"
                            className={`inputTextStyle ${s.inputText}`}
                            placeholder="Min"
                            pattern="[0-9]*"
                            name="minPrice"
                            maxLength={18}
                            onSelect={() => {
                                if (store.priceOption!==1) store.setPriceOption(1);
                            }}
                            onChange={async e => store.setPriceRange([e.target.value, store.priceRange[1]])}
                        />
                        <input
                            type="number"
                            className={`inputTextStyle ${s.inputText}`}
                            placeholder="Max"
                            pattern="[0-9]*"
                            name="maxPrice"
                            maxLength={18}
                            onSelect={() => {
                                if (store.priceOption!==1) store.setPriceOption(1);
                            }}
                            onChange={async e => store.setPriceRange([store.priceRange[0], e.target.value])}
                        />
                        <ActionButton className={`${s.goButton} ${store.priceOption !== 1 ? "disabled" : ""}`}
                                      buttonStyle={buttonStyles.newContinueButton} label="Go" onClick={async () => {
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;
                            store.RefreshCatalogItems(null, true);
                            await wait(0.75);
                            locked.current = false;
                        }}/>
                    </label>
                </div>
                <div className={`${s.priceContainer} radio2019`}>
                    <input
                        id="priceOption-2"
                        type="radio"
                        onChange={async e => {
                            
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;

                            store.setPriceOption(2)
                            await tick();
                            store.RefreshCatalogItems(null, true, { priceOption: 2 });
                            
                            await wait(0.75);
                            locked.current = false;
                        }}
                        checked={store.priceOption === 2}
                    />
                    <label htmlFor="priceOption-2">Free</label>
                </div>
            </div>
        </div>
        <div>
            <h5 className={s.filterHeader}>Unavailable Items</h5>
            <div className={s.priceFilters}>
                <div className={`${s.priceContainer} radio2019`}>
                    <input
                        id="unavailable-items-false"
                        type="radio"
                        onChange={async e => {
                            
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;

                            store.setOptions({...options, includeOffSale: false});
                            await tick();
                            store.RefreshCatalogItems(null, true, { includeOffSale: false });
                            
                            await wait(0.75);
                            locked.current = false;
                        }}
                        checked={!options.includeOffSale}
                    />
                    <label htmlFor="unavailable-items-false">Hide</label>
                </div>
                <div className={`${s.priceContainer} radio2019`}>
                    <input
                        id="unavailable-items-true"
                        type="radio"
                        onChange={async e => {
                            
                            if (locked.current || store.refreshDebounce.current) return;
                            locked.current = true;

                            store.setOptions({...options, includeOffSale: true});
                            await tick();
                            store.RefreshCatalogItems(null, true, { includeOffSale: true });
                            
                            await wait(0.75);
                            locked.current = false;
                        }}
                        checked={options.includeOffSale}
                    />
                    <label htmlFor="unavailable-items-true">Show</label>
                </div>
            </div>
        </div>
    </div>
}

export default CatalogFilters;
