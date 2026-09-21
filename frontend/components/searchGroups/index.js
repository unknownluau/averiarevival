import UserAdvertisement from "../userAdvertisement";
import Container from "./components/container";
import SearchGroupsStore from "./stores/searchGroupsStore";

const SearchGroups = props => {
  return <div className='container padding-none'>
    <div className='flex'>
      <div className='col-12 col-lg-12'>
        <div className='mb-4'>
          <UserAdvertisement type={1} />
        </div>
        <SearchGroupsStore.Provider>
          <Container keyword={props.keyword} />
        </SearchGroupsStore.Provider>
      </div>
    </div>
  </div>
}

export default SearchGroups;